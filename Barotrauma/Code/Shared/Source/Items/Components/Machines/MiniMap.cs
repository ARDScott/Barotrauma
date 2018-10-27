﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    partial class MiniMap : Powered
    {
        class HullData
        {
            public float? Oxygen;
            public float? Water;
        }
        
        private DateTime resetDataTime;

        private bool hasPower;

        private Dictionary<Hull, HullData> hullDatas;

        [Editable(ToolTip = "Does the machine require inputs from water detectors in order to show the water levels inside rooms."), Serialize(false, true)]
        public bool RequireWaterDetectors
        {
            get;
            set;
        }

        [Editable(ToolTip = "Does the machine require inputs from oxygen detectors in order to show the oxygen levels inside rooms."), Serialize(true, true)]
        public bool RequireOxygenDetectors
        {
            get;
            set;
        }

        [Editable(ToolTip = "Should damaged walls be displayed by the machine."), Serialize(true, true)]
        public bool ShowHullIntegrity
        {
            get;
            set;
        }

        public MiniMap(Item item, XElement element)
            : base(item, element)
        {
            IsActive = true;
            hullDatas = new Dictionary<Hull, HullData>();
            InitProjSpecific(element);
        }

        partial void InitProjSpecific(XElement element);

        public override void Update(float deltaTime, Camera cam) 
        {
            //periodically reset all hull data
            //(so that outdated hull info won't be shown if detectors stop sending signals)
            if (DateTime.Now > resetDataTime)
            {
                hullDatas.Clear();
                resetDataTime = DateTime.Now + new TimeSpan(0, 0, 1);
            }

            currPowerConsumption = powerConsumption;
            currPowerConsumption *= MathHelper.Lerp(2.0f, 1.0f, item.Condition / 100.0f);

            hasPower = voltage > minVoltage;
            if (hasPower)
            {
                ApplyStatusEffects(ActionType.OnActive, deltaTime, null);
            }

            voltage = 0.0f;
        }
        
        public override bool Pick(Character picker)
        {
            return picker != null;
        }

        public override void ReceiveSignal(int stepsTaken, string signal, Connection connection, Item source, Character sender, float power = 0, float signalStrength = 1.0f)
        {
            base.ReceiveSignal(stepsTaken, signal, connection, source, sender, power, signalStrength);

            if (source == null || source.CurrentHull == null) return;

            Hull sourceHull = source.CurrentHull;
            if (!hullDatas.TryGetValue(sourceHull, out HullData hullData))
            {
                hullData = new HullData();
                hullDatas.Add(sourceHull, hullData);
            }

            switch (connection.Name)
            {
                case "water_data_in":
                    //cheating a bit because water detectors don't actually send the water level
                    if (source.GetComponent<WaterDetector>() == null)
                    {
                        hullData.Water = Rand.Range(0.0f, 1.0f);
                    }
                    else
                    {
                        hullData.Water = Math.Min(sourceHull.WaterVolume / sourceHull.Volume, 1.0f);
                    }
                    break;
                case "oxygen_data_in":
                    float oxy;

                    if (!float.TryParse(signal, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out oxy))
                    {
                        oxy = Rand.Range(0.0f, 100.0f);
                    }

                    hullData.Oxygen = oxy;
                    break;
            }
        }

    }
}
