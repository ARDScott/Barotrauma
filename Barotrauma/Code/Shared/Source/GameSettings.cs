﻿using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Xml;
#if CLIENT
using Microsoft.Xna.Framework.Graphics;
using Barotrauma.Tutorials;
#endif
using System;

namespace Barotrauma
{
    public enum WindowMode
    {
        Windowed, Fullscreen, BorderlessWindowed
    }

    public enum LosMode
    {
        None,
        Transparent,
        Opaque
    }

    public partial class GameSettings
    {
        const string FilePath = "config.xml";

        public int GraphicsWidth { get; set; }
        public int GraphicsHeight { get; set; }

        public bool VSyncEnabled { get; set; }

        public bool EnableSplashScreen { get; set; }
        
        public int ParticleLimit { get; set; }

        public float LightMapScale { get; set; }
        public bool SpecularityEnabled { get; set; }
        public bool ChromaticAberrationEnabled { get; set; }

        private KeyOrMouse[] keyMapping;

        private WindowMode windowMode;

        private LosMode losMode;

        public List<string> jobPreferences;

        private bool useSteamMatchmaking;
        private bool requireSteamAuthentication;

#if DEBUG
        //steam functionality can be enabled/disabled in debug builds
        public bool UseSteam;
        public bool RequireSteamAuthentication
        {
            get { return requireSteamAuthentication && UseSteam; }
            set { requireSteamAuthentication = value; }
        }
        public bool UseSteamMatchmaking
        {
            get { return useSteamMatchmaking && UseSteam; }
            set { useSteamMatchmaking = value; }
        }
#else
        //steam functionality determined at compile time
        public bool UseSteam
        {
            get { return Steam.SteamManager.USE_STEAM; }
        }
        public bool RequireSteamAuthentication
        {
            get { return requireSteamAuthentication && Steam.SteamManager.USE_STEAM; }
            set { requireSteamAuthentication = value; }
        }
        public bool UseSteamMatchmaking
        {
            get { return useSteamMatchmaking && Steam.SteamManager.USE_STEAM; }
            set { useSteamMatchmaking = value; }
        }
#endif

        public WindowMode WindowMode
        {
            get { return windowMode; }
            set { windowMode = value; }
        }

        public List<string> JobPreferences
        {
            get { return jobPreferences; }
            set { jobPreferences = value; }
        }

        private int characterHeadIndex;
        public int CharacterHeadIndex
        {
            get { return characterHeadIndex; }
            set
            {
                if (value == characterHeadIndex) return;
                characterHeadIndex = value;
                Save();
            }
        }

        private Gender characterGender;
        public Gender CharacterGender
        {
            get { return characterGender; }
            set
            {
                if (value == characterGender) return;
                characterGender = value;
                Save();
            }
        }

        private bool unsavedSettings;
        public bool UnsavedSettings
        {
            get
            {
                return unsavedSettings;
            }
            private set
            {
                unsavedSettings = value;
#if CLIENT
                if (applyButton != null)
                {
                    //applyButton.Selected = unsavedSettings;
                    applyButton.Enabled = unsavedSettings;
                    applyButton.Text = unsavedSettings ? "Apply*" : "Apply";
                }
#endif
            }
        }

        private float soundVolume, musicVolume;

        public float SoundVolume
        {
            get { return soundVolume; }
            set
            {
                soundVolume = MathHelper.Clamp(value, 0.0f, 1.0f);
#if CLIENT
                if (GameMain.SoundManager!=null)
                {
                    GameMain.SoundManager.SetCategoryGainMultiplier("default",soundVolume);
                    GameMain.SoundManager.SetCategoryGainMultiplier("ui",soundVolume);
                    GameMain.SoundManager.SetCategoryGainMultiplier("waterambience",soundVolume);
                }
#endif
            }
        }

        public float MusicVolume
        {
            get { return musicVolume; }
            set
            {
                musicVolume = MathHelper.Clamp(value, 0.0f, 1.0f);
#if CLIENT
                SoundPlayer.MusicVolume = musicVolume;
#endif
            }
        }

        public string Language
        {
            get { return TextManager.Language; }
            set { TextManager.Language = value; }
        }

        public HashSet<ContentPackage> SelectedContentPackages { get; set; }

        public string   MasterServerUrl { get; set; }
        public bool     AutoCheckUpdates { get; set; }
        public bool     WasGameUpdated { get; set; }

        private string defaultPlayerName;
        public string   DefaultPlayerName
        {
            get
            {
                return defaultPlayerName ?? "";
            }
            set
            {
                if (defaultPlayerName != value)
                {
                    defaultPlayerName = value;
                    Save();
                }
            }
        }

        public LosMode LosMode
        {
            get { return losMode; }
            set { losMode = value; }
        }

        private const float MinHUDScale = 0.75f, MaxHUDScale = 1.25f;
        public static float HUDScale { get; set; } = 1.0f;
        private const float MinInventoryScale = 0.75f, MaxInventoryScale = 1.25f;
        public static float InventoryScale { get; set; } = 1.0f;

        public List<string> CompletedTutorialNames { get; private set; }

        public static bool VerboseLogging { get; set; }
        public static bool SaveDebugConsoleLogs { get; set; }

        private static bool sendUserStatistics;
        public static bool SendUserStatistics
        {
            get { return sendUserStatistics; }
            set
            {
                sendUserStatistics = value;
                GameMain.Config.Save();
            }
        }
        public static bool ShowUserStatisticsPrompt { get; set; }

        public GameSettings(string filePath)
        {
            SelectedContentPackages = new HashSet<ContentPackage>();

            ContentPackage.LoadAll(ContentPackage.Folder);
            CompletedTutorialNames = new List<string>();
            Load(filePath);
        }

        public void Load(string filePath)
        {
            XDocument doc = XMLExtensions.TryLoadXml(filePath);
            
            Language = doc.Root.GetAttributeString("language", "English");

            MasterServerUrl = doc.Root.GetAttributeString("masterserverurl", "");

            AutoCheckUpdates = doc.Root.GetAttributeBool("autocheckupdates", true);
            WasGameUpdated = doc.Root.GetAttributeBool("wasgameupdated", false);

            VerboseLogging = doc.Root.GetAttributeBool("verboselogging", false);
            SaveDebugConsoleLogs = doc.Root.GetAttributeBool("savedebugconsolelogs", false);
            if (doc.Root.Attribute("senduserstatistics") == null)
            {
                ShowUserStatisticsPrompt = true;
            }
            else
            {
                sendUserStatistics = doc.Root.GetAttributeBool("senduserstatistics", true);
            }

#if DEBUG
            UseSteam = doc.Root.GetAttributeBool("usesteam", true);
#endif

            if (doc == null)
            {
                GraphicsWidth = 1024;
                GraphicsHeight = 678;

                MasterServerUrl = "";

                SelectedContentPackages.Add(ContentPackage.List.Any() ? ContentPackage.List[0] : new ContentPackage(""));

                jobPreferences = new List<string>();
                foreach (JobPrefab job in JobPrefab.List)
                {
                    jobPreferences.Add(job.Identifier);
                }
                return;
            }

            XElement graphicsMode = doc.Root.Element("graphicsmode");
            GraphicsWidth   = graphicsMode.GetAttributeInt("width", 0);
            GraphicsHeight  = graphicsMode.GetAttributeInt("height", 0);
            VSyncEnabled    = graphicsMode.GetAttributeBool("vsync", true);

            XElement graphicsSettings = doc.Root.Element("graphicssettings");
            ParticleLimit               = graphicsSettings.GetAttributeInt("particlelimit", 1500);
            LightMapScale               = MathHelper.Clamp(graphicsSettings.GetAttributeFloat("lightmapscale", 0.5f), 0.1f, 1.0f);
            SpecularityEnabled          = graphicsSettings.GetAttributeBool("specularity", true);
            ChromaticAberrationEnabled  = graphicsSettings.GetAttributeBool("chromaticaberration", true);
            HUDScale                    = graphicsSettings.GetAttributeFloat("hudscale", 1.0f);
            InventoryScale              = graphicsSettings.GetAttributeFloat("inventoryscale", 1.0f);
            var losModeStr              = graphicsSettings.GetAttributeString("losmode", "Transparent");
            if (!Enum.TryParse(losModeStr, out losMode))
            {
                losMode = LosMode.Transparent;
            }

#if CLIENT
            if (GraphicsWidth == 0 || GraphicsHeight == 0)
            {
                GraphicsWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                GraphicsHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            }
#endif

            //FullScreenEnabled = ToolBox.GetAttributeBool(graphicsMode, "fullscreen", true);

            var windowModeStr = graphicsMode.GetAttributeString("displaymode", "Fullscreen");
            if (!Enum.TryParse<WindowMode>(windowModeStr, out windowMode))
            {
                windowMode = WindowMode.Fullscreen;
            }

            SoundVolume = doc.Root.GetAttributeFloat("soundvolume", 1.0f);
            MusicVolume = doc.Root.GetAttributeFloat("musicvolume", 0.3f);

            useSteamMatchmaking = doc.Root.GetAttributeBool("usesteammatchmaking", true);
            requireSteamAuthentication = doc.Root.GetAttributeBool("requiresteamauthentication", true);

            EnableSplashScreen = doc.Root.GetAttributeBool("enablesplashscreen", true);

            keyMapping = new KeyOrMouse[Enum.GetNames(typeof(InputType)).Length];
            keyMapping[(int)InputType.Up] = new KeyOrMouse(Keys.W);
            keyMapping[(int)InputType.Down] = new KeyOrMouse(Keys.S);
            keyMapping[(int)InputType.Left] = new KeyOrMouse(Keys.A);
            keyMapping[(int)InputType.Right] = new KeyOrMouse(Keys.D);
            keyMapping[(int)InputType.Run] = new KeyOrMouse(Keys.LeftShift);

            keyMapping[(int)InputType.Chat] = new KeyOrMouse(Keys.Tab);
            keyMapping[(int)InputType.RadioChat] = new KeyOrMouse(Keys.OemPipe);
            keyMapping[(int)InputType.CrewOrders] = new KeyOrMouse(Keys.C);

            keyMapping[(int)InputType.Select] = new KeyOrMouse(Keys.E);

            keyMapping[(int)InputType.SelectNextCharacter] = new KeyOrMouse(Keys.Tab);
            keyMapping[(int)InputType.SelectPreviousCharacter] = new KeyOrMouse(Keys.Q);

            keyMapping[(int)InputType.Use] = new KeyOrMouse(0);
            keyMapping[(int)InputType.Aim] = new KeyOrMouse(1);

            foreach (XElement subElement in doc.Root.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "keymapping":
                        foreach (XAttribute attribute in subElement.Attributes())
                        {
                            if (Enum.TryParse(attribute.Name.ToString(), true, out InputType inputType))
                            {
                                if (int.TryParse(attribute.Value.ToString(), out int mouseButton))
                                {
                                    keyMapping[(int)inputType] = new KeyOrMouse(mouseButton);
                                }
                                else
                                {
                                    if (Enum.TryParse(attribute.Value.ToString(), true, out Keys key))
                                    {
                                        keyMapping[(int)inputType] = new KeyOrMouse(key);
                                    }
                                }
                            }
                        }
                        break;
                    case "gameplay":
                        jobPreferences = new List<string>();
                        foreach (XElement ele in subElement.Element("jobpreferences").Elements("job"))
                        {
                            string jobIdentifier = ele.GetAttributeString("identifier", "");
                            if (string.IsNullOrEmpty(jobIdentifier)) continue;
                            jobPreferences.Add(jobIdentifier);
                        }
                        break;
                    case "player":
                        defaultPlayerName = subElement.GetAttributeString("name", "");
                        characterHeadIndex = subElement.GetAttributeInt("headindex", Rand.Int(10));
                        characterGender = subElement.GetAttributeString("gender", Rand.Range(0.0f, 1.0f) < 0.5f ? "male" : "female")
                            .ToLowerInvariant() == "male" ? Gender.Male : Gender.Female;
                        break;
                    case "tutorials":
                        foreach (XElement tutorialElement in subElement.Elements())
                        {
                            CompletedTutorialNames.Add(tutorialElement.GetAttributeString("name", ""));
                        }
                        break;
                }
            }

            foreach (InputType inputType in Enum.GetValues(typeof(InputType)))
            {
                if (keyMapping[(int)inputType] == null)
                {
                    DebugConsole.ThrowError("Key binding for the input type \"" + inputType + " not set!");
                    keyMapping[(int)inputType] = new KeyOrMouse(Keys.D1);
                }
            }
            
            UnsavedSettings = false;

            foreach (XElement subElement in doc.Root.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "contentpackage":
                        string path = subElement.GetAttributeString("path", "");
                        var matchingContentPackage = ContentPackage.List.Find(cp => cp.Path == path);
                        if (matchingContentPackage == null)
                        {
                            DebugConsole.ThrowError("Content package \"" + path + "\" not found!");
                        }
                        else
                        {
                            SelectedContentPackages.Add(matchingContentPackage);
                        }
                        break;
                }
            }
        }
        
        public void Save()
        {
            UnsavedSettings = false;

            XDocument doc = new XDocument();

            if (doc.Root == null)
            {
                doc.Add(new XElement("config"));
            }

            doc.Root.Add(
                new XAttribute("language", TextManager.Language),
                new XAttribute("masterserverurl", MasterServerUrl),
                new XAttribute("autocheckupdates", AutoCheckUpdates),
                new XAttribute("musicvolume", musicVolume),
                new XAttribute("soundvolume", soundVolume),
                new XAttribute("verboselogging", VerboseLogging),
                new XAttribute("savedebugconsolelogs", SaveDebugConsoleLogs),
                new XAttribute("enablesplashscreen", EnableSplashScreen),
                new XAttribute("usesteammatchmaking", useSteamMatchmaking),
                new XAttribute("requiresteamauthentication", requireSteamAuthentication));

            if (!ShowUserStatisticsPrompt)
            {
                doc.Root.Add(new XAttribute("senduserstatistics", sendUserStatistics));
            }

            if (WasGameUpdated)
            {
                doc.Root.Add(new XAttribute("wasgameupdated", true));
            }

            XElement gMode = doc.Root.Element("graphicsmode");
            if (gMode == null)
            {
                gMode = new XElement("graphicsmode");
                doc.Root.Add(gMode);
            }

            if (GraphicsWidth == 0 || GraphicsHeight == 0)
            {
                gMode.ReplaceAttributes(new XAttribute("displaymode", windowMode));
            }
            else
            {
                gMode.ReplaceAttributes(
                    new XAttribute("width", GraphicsWidth),
                    new XAttribute("height", GraphicsHeight),
                    new XAttribute("vsync", VSyncEnabled),
                    new XAttribute("displaymode", windowMode));
            }

            XElement gSettings = doc.Root.Element("graphicssettings");
            if (gSettings == null)
            {
                gSettings = new XElement("graphicssettings");
                doc.Root.Add(gSettings);
            }

            gSettings.ReplaceAttributes(
                new XAttribute("particlelimit", ParticleLimit),
                new XAttribute("lightmapscale", LightMapScale),
                new XAttribute("specularity", SpecularityEnabled),
                new XAttribute("chromaticaberration", ChromaticAberrationEnabled),
                new XAttribute("losmode", LosMode),
                new XAttribute("hudscale", HUDScale),
                new XAttribute("inventoryscale", InventoryScale));

            foreach (ContentPackage contentPackage in SelectedContentPackages)
            {
                doc.Root.Add(new XElement("contentpackage",
                    new XAttribute("path", contentPackage.Path)));
            }

            var keyMappingElement = new XElement("keymapping");
            doc.Root.Add(keyMappingElement);
            for (int i = 0; i < keyMapping.Length; i++)
            {
                if (keyMapping[i].MouseButton == null)
                {
                    keyMappingElement.Add(new XAttribute(((InputType)i).ToString(), keyMapping[i].Key));
                }
                else
                {
                    keyMappingElement.Add(new XAttribute(((InputType)i).ToString(), keyMapping[i].MouseButton));
                }
            }

            var gameplay = new XElement("gameplay");
            var jobPreferences = new XElement("jobpreferences");
            foreach (string jobName in JobPreferences)
            {
                jobPreferences.Add(new XElement("job", new XAttribute("identifier", jobName)));
            }
            gameplay.Add(jobPreferences);
            doc.Root.Add(gameplay);

            var playerElement = new XElement("player",
                new XAttribute("name", defaultPlayerName ?? ""),
                new XAttribute("headindex", characterHeadIndex),
                new XAttribute("gender", characterGender));
            doc.Root.Add(playerElement);
            
#if CLIENT
            if (Tutorial.Tutorials != null)
            {
                foreach (Tutorial tutorial in Tutorial.Tutorials)
                {
                    if (tutorial.Completed && !CompletedTutorialNames.Contains(tutorial.Name))
                    {
                        CompletedTutorialNames.Add(tutorial.Name);
                    }
                }
            }
#endif
            var tutorialElement = new XElement("tutorials");
            foreach (string tutorialName in CompletedTutorialNames)
            {
                tutorialElement.Add(new XElement("Tutorial", new XAttribute("name", tutorialName)));
            }
            doc.Root.Add(tutorialElement);
            
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = true,
                NewLineOnAttributes = true
            };

            try
            {
                using (var writer = XmlWriter.Create(FilePath, settings))
                {
                    doc.WriteTo(writer);
                    writer.Flush();
                }
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Saving game settings failed.", e);
                GameAnalyticsManager.AddErrorEventOnce("GameSettings.Save:SaveFailed", GameAnalyticsSDK.Net.EGAErrorSeverity.Error,
                    "Saving game settings failed.\n" + e.Message + "\n" + e.StackTrace);
            }
        }
    }
}
