﻿using Barotrauma.Networking;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    partial class Repairable : ItemComponent, IServerSerializable, IClientSerializable
    {
        public static float SkillIncreaseMultiplier = 0.4f;

        private string header;
        
        private float lastSentProgress;

        public bool Fixed
        {
            get { return repairProgress >= 1.0f; }
        }

        private float fixDurationLowSkill, fixDurationHighSkill;

        [Serialize(0.0f, true), Editable(MinValueFloat = 0.0f, MaxValueFloat = 100.0f, DecimalCount = 2, ToolTip = "How fast the condition of the item deteriorates per second.")]
        public float DeteriorationSpeed
        {
            get;
            set;
        }

        [Serialize(50.0f, true), Editable(MinValueFloat = 0.0f, MaxValueFloat = 100.0f, ToolTip = "The item won't deteriorate spontaneously if the condition is below this value. For example, if set to 10, the condition will spontaneously drop to 10 and then stop dropping (unless the item is damaged further by external factors).")]
        public float MinDeteriorationCondition
        {
            get;
            set;
        }

        [Serialize(80.0f, true), Editable(MinValueFloat = 0.0f, MaxValueFloat = 100.0f, ToolTip = "The condition of the item has to be below this before the repair UI becomes usable.")]
        public float ShowRepairUIThreshold
        {
            get;
            set;
        }

        private float repairProgress;
        public float RepairProgress
        {
            get { return repairProgress; }
            set
            {
                repairProgress = MathHelper.Clamp(value, 0.0f, 1.0f);
                if (repairProgress >= 1.0f && currentFixer != null) currentFixer.AnimController.Anim = AnimController.Animation.None;
            }
        }
        
        private Character currentFixer;
        public Character CurrentFixer
        {
            get { return currentFixer; }
            set
            {
                if (currentFixer == value || item.Condition >= 100.0f) return;
                if (currentFixer != null) currentFixer.AnimController.Anim = AnimController.Animation.None;
                currentFixer = value;
            }
        }
        
        public Repairable(Item item, XElement element) 
            : base(item, element)
        {
            IsActive = true;
            canBeSelected = true;

            this.item = item;
            header = element.GetAttributeString("name", "");
            fixDurationLowSkill = element.GetAttributeFloat("fixdurationlowskill", 100.0f);
            fixDurationHighSkill = element.GetAttributeFloat("fixdurationhighskill", 5.0f);

            InitProjSpecific(element);
        }
        
        partial void InitProjSpecific(XElement element);
        
        public void StartRepairing(Character character)
        {
            CurrentFixer = character;
        }

        public override void UpdateBroken(float deltaTime, Camera cam)
        {
            Update(deltaTime, cam);
        }

        public override void Update(float deltaTime, Camera cam)
        {
            if (CurrentFixer == null)
            {
                if (item.Condition <= 0.0f)
                {
                    repairProgress = 0.0f;
                }
                else
                {
                    if (item.Condition > MinDeteriorationCondition)
                    {
                        item.Condition -= DeteriorationSpeed * deltaTime;
                    }

                    float targetProgress = item.Condition / 100.0f;
                    repairProgress = targetProgress < repairProgress ? 
                        Math.Max(targetProgress, repairProgress - deltaTime * 0.1f) :
                        Math.Min(targetProgress, repairProgress + deltaTime * 0.1f);
                }
                return;
            }

            if (CurrentFixer.SelectedConstruction != item || !currentFixer.CanInteractWith(item))
            {
                currentFixer.AnimController.Anim = AnimController.Animation.None;
                currentFixer = null;
                return;
            }

            UpdateFixAnimation(CurrentFixer);

            if (GameMain.Client != null) return;

            float successFactor = requiredSkills.Count == 0 ? 1.0f : 0.0f;
            foreach (Skill skill in requiredSkills)
            {
                float characterSkillLevel = CurrentFixer.GetSkillLevel(skill.Identifier);
                if (characterSkillLevel >= skill.Level) successFactor += 1.0f / requiredSkills.Count;
                CurrentFixer.Info.IncreaseSkillLevel(skill.Identifier,
                    SkillIncreaseMultiplier * deltaTime / Math.Max(characterSkillLevel, 1.0f),
                     CurrentFixer.WorldPosition + Vector2.UnitY * 100.0f);
            }

            float fixDuration = MathHelper.Lerp(fixDurationLowSkill, fixDurationHighSkill, successFactor);
            if (fixDuration <= 0.0f)
            {
                repairProgress = 1.0f;
            }
            else
            {
                RepairProgress += deltaTime / fixDuration;
            }

            if (item.Repairables.All(r => r.Fixed))
            {
                item.Condition = 100.0f;
            }

            if (GameMain.Server != null && Math.Abs(RepairProgress - lastSentProgress) > 0.01f)
            {
                lastSentProgress = RepairProgress;
                item.CreateServerEvent(this);
            }

            if (Fixed)
            {
                SteamAchievementManager.OnItemRepaired(item, currentFixer);
            }
        }


        private void UpdateFixAnimation(Character character)
        {
            character.AnimController.UpdateUseItem(false, item.SimPosition + Vector2.UnitY * (repairProgress % 0.1f));
        }

        public void ServerWrite(NetBuffer msg, Client c, object[] extraData = null)
        {
            msg.Write(repairProgress);
        }

        public void ClientRead(ServerNetObject type, NetBuffer msg, float sendingTime)
        {
            repairProgress = msg.ReadSingle();
        }

        public void ClientWrite(NetBuffer msg, object[] extraData = null)
        {
            //no need to write anything, just letting the server know we started repairing
        }

        public void ServerRead(ClientNetObject type, NetBuffer msg, Client c)
        {
            if (c.Character == null) return;
            StartRepairing(c.Character);
        }
    }
}
