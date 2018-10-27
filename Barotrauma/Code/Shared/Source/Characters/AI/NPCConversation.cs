﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma
{
    class NPCConversation
    {
        const int MaxPreviousConversations = 20;

        private static List<NPCConversation> list = new List<NPCConversation>();
        
        public readonly string Line;

        public readonly List<JobPrefab> AllowedJobs;

        public readonly List<string> Flags;

        //The line can only be selected when eventmanager intensity is between these values
        //null = no restriction
        public float? maxIntensity, minIntensity;

        public readonly List<NPCConversation> Responses;
        private readonly int speakerIndex;
        private readonly List<string> allowedSpeakerTags;
        public static void LoadAll(IEnumerable<string> filePaths)
        {
            //language, identifier, filepath
            List<Tuple<string, string, string>> contentPackageFiles = new List<Tuple<string, string, string>>();
            foreach (string filePath in filePaths)
            {
                XDocument doc = XMLExtensions.TryLoadXml(filePath);
                if (doc == null || doc.Root == null) continue;
                string language = doc.Root.GetAttributeString("Language", "English");
                string identifier = doc.Root.GetAttributeString("Identifier", "unknown");
                contentPackageFiles.Add(new Tuple<string, string, string>(language, identifier, filePath));
            }

            List<Tuple<string, string, string>> translationFiles = new List<Tuple<string, string, string>>();
            foreach (string filePath in Directory.GetFiles(Path.Combine("Content", "NPCConversations")))
            {
                XDocument doc = XMLExtensions.TryLoadXml(filePath);
                if (doc == null || doc.Root == null) continue;
                string language = doc.Root.GetAttributeString("Language", "English");
                string identifier = doc.Root.GetAttributeString("Identifier", "unknown");
                translationFiles.Add(new Tuple<string, string, string>(language, identifier, filePath));
            }


            //get the languages and identifiers of the files
            for (int i = 0; i < contentPackageFiles.Count; i++)
            {
                var contentPackageFile = contentPackageFiles[i];
                //correct language, all good
                if (contentPackageFile.Item1 == TextManager.Language) continue;

                //attempt to find a translation file with the correct language and a matching identifier
                //if it fails, we'll just use the original file with the incorrect language
                var translation = translationFiles.Find(t => t.Item1 == TextManager.Language && t.Item2 == contentPackageFile.Item2);
                if (translation != null) contentPackageFiles[i] = translation; //replace with the translation file                
            }

            foreach (var file in contentPackageFiles)
            {
                Load(file.Item3);
            }
        }

        private static void Load(string file)
        {
            XDocument doc = XMLExtensions.TryLoadXml(file);
            if (doc == null || doc.Root == null) return;

            string language = doc.Root.GetAttributeString("Language", "English");
            if (language != TextManager.Language) return;

            foreach (XElement subElement in doc.Root.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "conversation":
                        list.Add(new NPCConversation(subElement));
                        break;
                    case "personalitytrait":
                        new NPCPersonalityTrait(subElement);
                        break;
                }
            }
        }

        public NPCConversation(XElement element)
        {
            Line = element.GetAttributeString("line", "");

            speakerIndex = element.GetAttributeInt("speaker", 0);

            AllowedJobs = new List<JobPrefab>();
            string allowedJobsStr = element.GetAttributeString("allowedjobs", "");
            foreach (string allowedJobIdentifier in allowedJobsStr.Split(','))
            {
                var jobPrefab = JobPrefab.List.Find(jp => jp.Identifier.ToLowerInvariant() == allowedJobIdentifier.ToLowerInvariant());
                if (jobPrefab != null) AllowedJobs.Add(jobPrefab);
            }

            Flags = new List<string>(element.GetAttributeStringArray("flags", new string[0]));

            allowedSpeakerTags = new List<string>();
            string allowedSpeakerTagsStr = element.GetAttributeString("speakertags", "");
            foreach (string tag in allowedSpeakerTagsStr.Split(','))
            {
                if (string.IsNullOrEmpty(tag)) continue;
                allowedSpeakerTags.Add(tag.Trim().ToLowerInvariant());                
            }

            if (element.Attribute("minintensity") != null) minIntensity = element.GetAttributeFloat("minintensity", 0.0f);
            if (element.Attribute("maxintensity") != null) maxIntensity = element.GetAttributeFloat("maxintensity", 1.0f);

            Responses = new List<NPCConversation>();
            foreach (XElement subElement in element.Elements())
            {
                Responses.Add(new NPCConversation(subElement));
            }
        }

        private static List<string> GetCurrentFlags(Character speaker)
        {
            var currentFlags = new List<string>();
            if (Submarine.MainSub != null && Submarine.MainSub.AtDamageDepth) currentFlags.Add("SubmarineDeep");
            if (GameMain.GameSession != null && Timing.TotalTime < GameMain.GameSession.RoundStartTime + 30.0f) currentFlags.Add("Initial");
            if (speaker != null)
            {
                if (speaker.AnimController.InWater) currentFlags.Add("Underwater");
                currentFlags.Add(speaker.CurrentHull == null ? "Outside" : "Inside");

                var afflictions = speaker.CharacterHealth.GetAllAfflictions();
                foreach (Affliction affliction in afflictions)
                {
                    var currentEffect = affliction.Prefab.GetActiveEffect(affliction.Strength);
                    if (currentEffect != null && !string.IsNullOrEmpty(currentEffect.DialogFlag) && !currentFlags.Contains(currentEffect.DialogFlag))
                    {
                        currentFlags.Add(currentEffect.DialogFlag);
                    }
                }
            }

            return currentFlags;
        }

        private static List<NPCConversation> previousConversations = new List<NPCConversation>();
        
        public static List<Pair<Character, string>> CreateRandom(List<Character> availableSpeakers)
        {
            Dictionary<int, Character> assignedSpeakers = new Dictionary<int, Character>();
            List<Pair<Character, string>> lines = new List<Pair<Character, string>>();

            CreateConversation(availableSpeakers, assignedSpeakers, null, lines);
            return lines;
        }

        private static void CreateConversation(
            List<Character> availableSpeakers, 
            Dictionary<int, Character> assignedSpeakers, 
            NPCConversation baseConversation, 
            List<Pair<Character, string>> lineList)
        {
            List<NPCConversation> conversations = baseConversation == null ? list : baseConversation.Responses;
            if (conversations.Count == 0) return;

            int conversationIndex = Rand.Int(conversations.Count);
            NPCConversation selectedConversation = conversations[conversationIndex];
            if (string.IsNullOrEmpty(selectedConversation.Line)) return;

            Character speaker = null;
            //speaker already assigned for this line
            if (assignedSpeakers.ContainsKey(selectedConversation.speakerIndex))
            {
                speaker = assignedSpeakers[selectedConversation.speakerIndex];
            }
            else
            {
                var allowedSpeakers = new List<Character>();

                List<NPCConversation> potentialLines = new List<NPCConversation>(conversations);

                //remove lines that are not appropriate for the intensity of the current situation
                if (GameMain.GameSession?.EventManager != null)
                {
                    potentialLines.RemoveAll(l => 
                        (l.minIntensity.HasValue && GameMain.GameSession.EventManager.CurrentIntensity < l.minIntensity) ||
                        (l.maxIntensity.HasValue && GameMain.GameSession.EventManager.CurrentIntensity > l.maxIntensity));
                }

                while (potentialLines.Count > 0)
                {
                    //select a random line and attempt to find a speaker for it
                    // and if no valid speaker is found, choose another random line
                    selectedConversation = GetRandomConversation(potentialLines, baseConversation == null);
                    if (selectedConversation == null || string.IsNullOrEmpty(selectedConversation.Line)) return;
                    
                    //speaker already assigned for this line
                    if (assignedSpeakers.ContainsKey(selectedConversation.speakerIndex))
                    {
                        speaker = assignedSpeakers[selectedConversation.speakerIndex];
                        break;
                    }

                    foreach (Character potentialSpeaker in availableSpeakers)
                    {
                        //check if the character has an appropriate job to say the line
                        if (selectedConversation.AllowedJobs.Count > 0 && !selectedConversation.AllowedJobs.Contains(potentialSpeaker.Info?.Job.Prefab)) continue;

                        //check if the character has all required flags to say the line
                        var characterFlags = GetCurrentFlags(potentialSpeaker);
                        if (!selectedConversation.Flags.All(flag => characterFlags.Contains(flag))) continue;

                        //check if the character has an appropriate personality
                        if (selectedConversation.allowedSpeakerTags.Count > 0)
                        {
                            if (potentialSpeaker.Info?.PersonalityTrait == null) continue;
                            if (!selectedConversation.allowedSpeakerTags.Any(t => potentialSpeaker.Info.PersonalityTrait.AllowedDialogTags.Any(t2 => t2 == t))) continue;
                        }
                        else
                        {
                            if (potentialSpeaker.Info?.PersonalityTrait != null &&
                                !potentialSpeaker.Info.PersonalityTrait.AllowedDialogTags.Contains("none"))
                            {
                                continue;
                            }
                        }

                        allowedSpeakers.Add(potentialSpeaker);
                    }

                    if (allowedSpeakers.Count == 0)
                    {
                        potentialLines.Remove(selectedConversation);
                    }
                    else
                    {
                        break;
                    }
                }

                if (allowedSpeakers.Count == 0) return;
                speaker = allowedSpeakers[Rand.Int(allowedSpeakers.Count)];
                availableSpeakers.Remove(speaker);
                assignedSpeakers.Add(selectedConversation.speakerIndex, speaker);
            }

            if (baseConversation == null)
            {
                previousConversations.Insert(0, selectedConversation);
                if (previousConversations.Count > MaxPreviousConversations) previousConversations.RemoveAt(MaxPreviousConversations);
            }
            lineList.Add(new Pair<Character, string>(speaker, selectedConversation.Line));
            CreateConversation(availableSpeakers, assignedSpeakers, selectedConversation, lineList);
        }

        private static NPCConversation GetRandomConversation(List<NPCConversation> conversations, bool avoidPreviouslyUsed)
        {
            if (!avoidPreviouslyUsed)
            {
                return conversations.Count == 0 ? null : conversations[Rand.Int(conversations.Count)];
            }

            List<float> probabilities = new List<float>();
            foreach (NPCConversation conversation in conversations)
            {
                probabilities.Add(GetConversationProbability(conversation));
            }
            return ToolBox.SelectWeightedRandom(conversations, probabilities, Rand.RandSync.Unsynced);
        }

        private static float GetConversationProbability(NPCConversation conversation)
        {
            int index = previousConversations.IndexOf(conversation);
            if (index < 0) return 10.0f;

            return 1.0f - 1.0f / (index + 1);
        }
    }

}
