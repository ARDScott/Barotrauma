﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Barotrauma
{
    class ScriptedEventSet
    {
        public static List<ScriptedEventSet> List
        {
            get;
            private set;
        }

        //0-100
        public readonly float MinLevelDifficulty, MaxLevelDifficulty;

        //public readonly XElement ConfigElement;

        public readonly bool ChooseRandom;

        public readonly float MinDistanceTraveled;
        public readonly float MinMissionTime;

        public readonly Dictionary<string, float> Commonness;

        public readonly List<ScriptedEventPrefab> EventPrefabs;

        public readonly List<ScriptedEventSet> ChildSets;

        private ScriptedEventSet(XElement element)
        {
            Commonness = new Dictionary<string, float>();
            EventPrefabs = new List<ScriptedEventPrefab>();
            ChildSets = new List<ScriptedEventSet>();

            MinLevelDifficulty = element.GetAttributeFloat("minleveldifficulty", 0);
            MaxLevelDifficulty = Math.Max(element.GetAttributeFloat("maxleveldifficulty", 100), MinLevelDifficulty);

            ChooseRandom = element.GetAttributeBool("chooserandom", true);
            MinDistanceTraveled = element.GetAttributeFloat("mindistancetraveled", 0.0f);
            MinMissionTime = element.GetAttributeFloat("minmissiontime", 0.0f);

            Commonness[""] = 1.0f;
            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "commonness":
                        Commonness[""] = subElement.GetAttributeFloat("commonness", 0.0f);
                        foreach (XElement overrideElement in subElement.Elements())
                        {
                            if (overrideElement.Name.ToString().ToLowerInvariant() == "override")
                            {
                                string levelType = overrideElement.GetAttributeString("leveltype", "");
                                if (!Commonness.ContainsKey(levelType))
                                {
                                    Commonness.Add(levelType, overrideElement.GetAttributeFloat("commonness", 0.0f));
                                }
                            }
                        }
                        break;
                    case "eventset":
                        ChildSets.Add(new ScriptedEventSet(subElement));
                        break;
                    default:
                        EventPrefabs.Add(new ScriptedEventPrefab(subElement));
                        break;
                }
            }
        }

        public float GetCommonness(Level level)
        {
            return Commonness.ContainsKey(level.GenerationParams.Name) ?
                    Commonness[level.GenerationParams.Name] : Commonness[""];
        }

        public static void LoadPrefabs()
        {
            List = new List<ScriptedEventSet>();
            var configFiles = GameMain.Instance.GetFilesOfType(ContentType.RandomEvents);

            if (!configFiles.Any())
            {
                DebugConsole.ThrowError("No config files for random events found in the selected content package");
                return;
            }

            foreach (string configFile in configFiles)
            {
                XDocument doc = XMLExtensions.TryLoadXml(configFile);
                if (doc == null) continue;

                foreach (XElement element in doc.Root.Elements())
                {
                    if (element.Name.ToString().ToLowerInvariant() != "eventset") continue;
                    List.Add(new ScriptedEventSet(element));
                }
            }
        }
    }
}
