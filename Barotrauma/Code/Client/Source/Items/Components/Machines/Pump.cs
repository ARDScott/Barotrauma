﻿using Barotrauma.Networking;
using Barotrauma.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    partial class Pump : Powered, IServerSerializable, IClientSerializable
    {
        private GUIScrollBar isActiveSlider;
        private GUIScrollBar pumpSpeedSlider;
        private GUITickBox powerIndicator;

        private List<Pair<Vector2, ParticleEmitter>> pumpOutEmitters = new List<Pair<Vector2, ParticleEmitter>>(); 
        private List<Pair<Vector2, ParticleEmitter>> pumpInEmitters = new List<Pair<Vector2, ParticleEmitter>>(); 

        partial void InitProjSpecific(XElement element)
        {
            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "pumpoutemitter":
                        pumpOutEmitters.Add(new Pair<Vector2, ParticleEmitter>(
                            subElement.GetAttributeVector2("position", Vector2.Zero), 
                            new ParticleEmitter(subElement)));
                        break;
                    case "pumpinemitter":
                        pumpInEmitters.Add(new Pair<Vector2, ParticleEmitter>(
                            subElement.GetAttributeVector2("position", Vector2.Zero),
                            new ParticleEmitter(subElement)));
                        break;
                }
            }

            GUIFrame paddedFrame = new GUIFrame(new RectTransform(new Vector2(0.9f, 0.8f), GuiFrame.RectTransform, Anchor.Center), style: null);

            isActiveSlider = new GUIScrollBar(new RectTransform(new Point(50, 100), paddedFrame.RectTransform, Anchor.CenterLeft),
                barSize: 0.2f, style: "OnOffLever")
            {
                IsBooleanSwitch = true,
                MinValue = 0.25f,
                MaxValue = 0.75f
            };
            var sliderHandle = isActiveSlider.GetChild<GUIButton>();
            sliderHandle.RectTransform.NonScaledSize = new Point(84, sliderHandle.Rect.Height);
            
            isActiveSlider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                bool active = scrollBar.BarScroll < 0.5f;
                if (active == IsActive) return false;

                targetLevel = null;
                IsActive = active;
                if (!IsActive) currPowerConsumption = 0.0f;

                if (GameMain.Server != null)
                {
                    item.CreateServerEvent(this);
                    GameServer.Log(Character.Controlled.LogName + (IsActive ? " turned on " : " turned off ") + item.Name, ServerLog.MessageType.ItemInteraction);
                }
                else if (GameMain.Client != null)
                {
                    correctionTimer = CorrectionDelay;
                    item.CreateClientEvent(this);
                }

                return true;
            };

            var rightArea = new GUILayoutGroup(new RectTransform(new Vector2(0.75f, 1.0f), paddedFrame.RectTransform, Anchor.CenterRight)) { RelativeSpacing = 0.1f };

            powerIndicator = new GUITickBox(new RectTransform(new Point(30, 30), rightArea.RectTransform), TextManager.Get("PumpPowered"), style: "IndicatorLightGreen")
            {
                CanBeFocused = false
            };

            var pumpSpeedText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.2f), rightArea.RectTransform) { RelativeOffset = new Vector2(0.25f, 0.0f) },
                "", textAlignment: Alignment.BottomLeft);
            string pumpSpeedStr = TextManager.Get("PumpSpeed");
            pumpSpeedText.TextGetter = () => { return pumpSpeedStr + ": " + (int)flowPercentage + " %"; };

            var sliderArea = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.3f), rightArea.RectTransform, Anchor.CenterLeft), isHorizontal: true)
            {
                Stretch = true,
                RelativeSpacing = 0.05f
            };

            new GUITextBlock(new RectTransform(new Vector2(0.15f, 1.0f), sliderArea.RectTransform), 
                TextManager.Get("PumpOut"), textAlignment: Alignment.Center);
            pumpSpeedSlider = new GUIScrollBar(new RectTransform(new Vector2(0.8f, 1.0f), sliderArea.RectTransform), barSize: 0.25f, style: "GUISlider")
            {
                Step = 0.05f,
                OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
                {
                    float newValue = barScroll * 200.0f - 100.0f;
                    if (Math.Abs(newValue - FlowPercentage) < 0.1f) return false;

                    FlowPercentage = newValue;
                    if (GameMain.Server != null)
                    {
                        item.CreateServerEvent(this);
                        GameServer.Log(Character.Controlled.LogName + " set the pumping speed of " + item.Name + " to " + (int)(flowPercentage) + " %", ServerLog.MessageType.ItemInteraction);
                    }
                    else if (GameMain.Client != null)
                    {
                        correctionTimer = CorrectionDelay;
                        item.CreateClientEvent(this);
                    }
                    return true;
                }
            };

            new GUITextBlock(new RectTransform(new Vector2(0.15f, 1.0f), sliderArea.RectTransform), 
                TextManager.Get("PumpIn"), textAlignment: Alignment.Center);            
        }

        partial void UpdateProjSpecific(float deltaTime)
        {
            if (FlowPercentage < 0.0f)
            {
                foreach (Pair<Vector2, ParticleEmitter> pumpOutEmitter in pumpOutEmitters)
                {
                    //only emit "pump out" particles when underwater
                    Vector2 particlePos = item.Rect.Location.ToVector2() + pumpOutEmitter.First;
                    if (item.CurrentHull != null && item.CurrentHull.Surface < particlePos.Y) continue;

                    pumpOutEmitter.Second.Emit(deltaTime, item.WorldRect.Location.ToVector2() + pumpOutEmitter.First, item.CurrentHull,
                        velocityMultiplier: MathHelper.Lerp(0.5f, 1.0f, -FlowPercentage / 100.0f));
                }
            }
            else if (FlowPercentage > 0.0f)
            {
                foreach (Pair<Vector2, ParticleEmitter> pumpInEmitter in pumpInEmitters)
                {
                    pumpInEmitter.Second.Emit(deltaTime, item.WorldRect.Location.ToVector2() + pumpInEmitter.First, item.CurrentHull,
                        velocityMultiplier: MathHelper.Lerp(0.5f, 1.0f, FlowPercentage / 100.0f));
                }
            }
        }

        public override void UpdateHUD(Character character, float deltaTime, Camera cam)
        {
            powerIndicator.Selected = hasPower && IsActive;

            if (!PlayerInput.LeftButtonHeld())
            {
                isActiveSlider.BarScroll += (IsActive ? -10.0f : 10.0f) * deltaTime;

                float pumpSpeedScroll = (FlowPercentage + 100.0f) / 200.0f;
                if (Math.Abs(pumpSpeedScroll - pumpSpeedSlider.BarScroll) > 0.01f)
                {
                    pumpSpeedSlider.BarScroll = pumpSpeedScroll;
                }
            }
        }
        
        public void ClientWrite(Lidgren.Network.NetBuffer msg, object[] extraData = null)
        {
            //flowpercentage can only be adjusted at 10% intervals -> no need for more accuracy than this
            msg.WriteRangedInteger(-10, 10, (int)(flowPercentage / 10.0f));
            msg.Write(IsActive);
        }

        public void ClientRead(ServerNetObject type, Lidgren.Network.NetBuffer msg, float sendingTime)
        {
            if (correctionTimer > 0.0f)
            {
                StartDelayedCorrection(type, msg.ExtractBits(5 + 1), sendingTime);
                return;
            }

            FlowPercentage = msg.ReadRangedInteger(-10, 10) * 10.0f;
            IsActive = msg.ReadBoolean();
        }
    }
}
