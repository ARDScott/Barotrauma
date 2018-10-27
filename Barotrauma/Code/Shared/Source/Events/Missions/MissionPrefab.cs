﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;

namespace Barotrauma
{
    enum MissionType
    {
        Random,
        None,
        Salvage,
        Monster,
        Cargo,
        Combat
    }

    class MissionPrefab
    {
        public static readonly List<MissionPrefab> List = new List<MissionPrefab>();

        private static readonly Dictionary<MissionType, Type> missionClasses = new Dictionary<MissionType, Type>()
        {
            { MissionType.Salvage, typeof(SalvageMission) },
            { MissionType.Monster, typeof(MonsterMission) },
            { MissionType.Cargo, typeof(CargoMission) },
            { MissionType.Combat, typeof(CombatMission) },
        };
        
        private ConstructorInfo constructor;

        public readonly MissionType type;

        public readonly bool MultiplayerOnly, SingleplayerOnly;

        public readonly string Identifier;

        public readonly string Name;
        public readonly string Description;
        public readonly string SuccessMessage;
        public readonly string FailureMessage;
        public readonly string SonarLabel;

        public readonly string AchievementIdentifier;

        public readonly int Commonness;

        public readonly int Reward;

        public readonly List<string> Headers;
        public readonly List<string> Messages;

        //the mission can only be received when travelling from Pair.First to Pair.Second
        public readonly List<Pair<string, string>> AllowedLocationTypes;

        public readonly XElement ConfigElement;

        public static void Init()
        {
            var files = GameMain.Instance.GetFilesOfType(ContentType.Missions);
            foreach (string file in files)
            {
                XDocument doc = XMLExtensions.TryLoadXml(file);
                if (doc?.Root == null) continue;

                foreach (XElement element in doc.Root.Elements())
                {
                    List.Add(new MissionPrefab(element));
                }
            }
        }

        public MissionPrefab(XElement element)
        {
            ConfigElement = element;

            Identifier = element.GetAttributeString("identifier", "");

            Name        = TextManager.Get("MissionName." + Identifier, true) ?? element.GetAttributeString("name", "");
            Description = TextManager.Get("MissionDescription." + Identifier, true) ?? element.GetAttributeString("description", "");
            Reward      = element.GetAttributeInt("reward", 1);

            Commonness  = element.GetAttributeInt("commonness", 1);

            SuccessMessage  = TextManager.Get("MissionSuccess." + Identifier, true) ?? element.GetAttributeString("successmessage", "Mission completed successfully");
            FailureMessage  = TextManager.Get("MissionFailure." + Identifier, true) ?? element.GetAttributeString("failuremessage", "Mission failed");

            SonarLabel      = TextManager.Get("MissionSonarLabel." + Identifier, true) ?? element.GetAttributeString("sonarlabel", "");

            MultiplayerOnly     = element.GetAttributeBool("multiplayeronly", false);
            SingleplayerOnly    = element.GetAttributeBool("singleplayeronly", false);

            AchievementIdentifier = element.GetAttributeString("achievementidentifier", "");

            Headers = new List<string>();
            Messages = new List<string>();
            AllowedLocationTypes = new List<Pair<string, string>>();
            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "message":
                        int index = Messages.Count;

                        Headers.Add(TextManager.Get("MissionHeader" + index + "." + Identifier, true) ?? subElement.GetAttributeString("header", ""));
                        Messages.Add(TextManager.Get("MissionMessage" + index + "." + Identifier, true) ?? subElement.GetAttributeString("text", ""));
                        break;
                    case "locationtype":
                        AllowedLocationTypes.Add(new Pair<string, string>(
                            subElement.GetAttributeString("from", ""),
                            subElement.GetAttributeString("to", "")));
                        break;
                }
            }

            string missionTypeName = element.GetAttributeString("type", "");
            if (!Enum.TryParse(missionTypeName, out type))
            {
                DebugConsole.ThrowError("Error in mission prefab \"" + Name + "\" - \"" + missionTypeName + "\" is not a valid mission type.");
                return;
            }
            if (type == MissionType.Random)
            {
                DebugConsole.ThrowError("Error in mission prefab \"" + Name + "\" - mission type cannot be random.");
                return;
            }
            if (type == MissionType.None)
            {
                DebugConsole.ThrowError("Error in mission prefab \"" + Name + "\" - mission type cannot be none.");
                return;
            }

            constructor = missionClasses[type].GetConstructor(new[] { typeof(MissionPrefab), typeof(Location[]) });
        }

        public bool IsAllowed(Location from, Location to)
        {
            foreach (Pair<string, string> allowedLocationType in AllowedLocationTypes)
            {
                if (allowedLocationType.First.ToLowerInvariant() == "any" ||
                    allowedLocationType.First.ToLowerInvariant() == from.Type.Name.ToLowerInvariant())
                {
                    if (allowedLocationType.Second.ToLowerInvariant() == "any" ||
                        allowedLocationType.Second.ToLowerInvariant() == to.Type.Name.ToLowerInvariant())
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        
        public Mission Instantiate(Location[] locations)
        {
            return constructor?.Invoke(new object[] { this, locations }) as Mission;
        }
    }
}
