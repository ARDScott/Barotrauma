﻿using Barotrauma.Networking;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    partial class Reactor : Powered, IServerSerializable, IClientSerializable
    {
        const float NetworkUpdateInterval = 0.5f;

        //the rate at which the reactor is being run on (higher rate -> higher temperature)
        private float fissionRate;
        
        //how much of the generated steam is used to spin the turbines and generate power
        private float turbineOutput;
        
        private float temperature;
        
        //is automatic temperature control on
        //(adjusts the fission rate and turbine output automatically to keep the
        //amount of power generated balanced with the load)
        private bool autoTemp;

        private Client BlameOnBroken;

        //automatical adjustment to the power output when 
        //turbine output and temperature are in the optimal range
        private float autoAdjustAmount;
        
        private float fuelConsumptionRate;

        private float meltDownTimer, meltDownDelay;
        private float fireTimer, fireDelay;

        private float maxPowerOutput;

        private float load;
        
        private bool unsentChanges;
        private float sendUpdateTimer;

        private float degreeOfSuccess;

        private float? nextServerLogWriteTime;
        private float lastServerLogWriteTime;

        private Vector2 optimalTemperature, allowedTemperature;
        private Vector2 optimalFissionRate, allowedFissionRate;
        private Vector2 optimalTurbineOutput, allowedTurbineOutput;

        private bool shutDown;

        const float AIUpdateInterval = 1.0f;
        private float aiUpdateTimer;

        private Character lastUser;
        private Character LastUser
        {
            get { return lastUser; }
            set
            {
                if (lastUser == value) return;
                lastUser = value;
                degreeOfSuccess = lastUser == null ? 0.0f : DegreeOfSuccess(lastUser);
            }
        }
        
        [Editable(0.0f, float.MaxValue, ToolTip = "How much power (kW) the reactor generates when operating at full capacity."), Serialize(10000.0f, true)]
        public float MaxPowerOutput
        {
            get { return maxPowerOutput; }
            set
            {
                maxPowerOutput = Math.Max(0.0f, value);
            }
        }
        
        [Editable(0.0f, float.MaxValue, ToolTip = "How long the temperature has to stay critical until a meltdown occurs."), Serialize(30.0f, true)]
        public float MeltdownDelay
        {
            get { return meltDownDelay; }
            set { meltDownDelay = Math.Max(value, 0.0f); }
        }

        [Editable(0.0f, float.MaxValue, ToolTip = "How long the temperature has to stay critical until the reactor catches fire."), Serialize(10.0f, true)]
        public float FireDelay
        {
            get { return fireDelay; }
            set { fireDelay = Math.Max(value, 0.0f); }
        }

        [Serialize(0.0f, true)]
        public float Temperature
        {
            get { return temperature; }
            set
            {
                if (!MathUtils.IsValid(value)) return;
                temperature = MathHelper.Clamp(value, 0.0f, 100.0f);
            }
        }

        [Serialize(0.0f, true)]
        public float FissionRate
        {
            get { return fissionRate; }
            set 
            {
                if (!MathUtils.IsValid(value)) return;
                fissionRate = MathHelper.Clamp(value, 0.0f, 100.0f); 
            }
        }

        [Serialize(0.0f, true)]
        public float TurbineOutput
        {
            get { return turbineOutput; }
            set 
            {
                if (!MathUtils.IsValid(value)) return;
                turbineOutput = MathHelper.Clamp(value, 0.0f, 100.0f); 
            }
        }
        
        [Serialize(0.2f, true), Editable(0.0f, 1000.0f, ToolTip = "How fast the condition of the contained fuel rods deteriorates.")]
        public float FuelConsumptionRate
        {
            get { return fuelConsumptionRate; }
            set
            {
                if (!MathUtils.IsValid(value)) return;
                fuelConsumptionRate = Math.Max(value, 0.0f);
            }
        }

        private float correctTurbineOutput;

        private float targetFissionRate;
        private float targetTurbineOutput;
        
        [Serialize(false, true)]
        public bool AutoTemp
        {
            get { return autoTemp; }
            set 
            { 
                autoTemp = value;
#if CLIENT
                if (autoTempSlider != null) 
                {
                    autoTempSlider.BarScroll = value ? 
                        Math.Min(0.45f, autoTempSlider.BarScroll) : 
                        Math.Max(0.55f, autoTempSlider.BarScroll);
                }
#endif
            }
        }
        
        private float prevAvailableFuel;
        public float AvailableFuel { get; set; }
        
        public Reactor(Item item, XElement element)
            : base(item, element)
        {         
            IsActive = true;
            InitProjSpecific(element);
        }

        partial void InitProjSpecific(XElement element);
                
        public override void Update(float deltaTime, Camera cam)
        {
            if (GameMain.Server != null && nextServerLogWriteTime != null)
            {
                if (Timing.TotalTime >= (float)nextServerLogWriteTime)
                {
                    GameServer.Log(lastUser.LogName + " adjusted reactor settings: " +
                            "Temperature: " + (int)(temperature * 100.0f) +
                            ", Fission rate: " + (int)targetFissionRate +
                            ", Turbine output: " + (int)targetTurbineOutput +
                            (autoTemp ? ", Autotemp ON" : ", Autotemp OFF"),
                            ServerLog.MessageType.ItemInteraction);

                    nextServerLogWriteTime = null;
                    lastServerLogWriteTime = (float)Timing.TotalTime;
                }
            }

            prevAvailableFuel = AvailableFuel;
            ApplyStatusEffects(ActionType.OnActive, deltaTime, null);

            //use a smoothed "correct output" instead of the actual correct output based on the load
            //so the player doesn't have to keep adjusting the rate impossibly fast when the load fluctuates heavily
            correctTurbineOutput += MathHelper.Clamp((load / MaxPowerOutput * 100.0f) - correctTurbineOutput, -10.0f, 10.0f) * deltaTime;

            //calculate tolerances of the meters based on the skills of the user
            //more skilled characters have larger "sweet spots", making it easier to keep the power output at a suitable level
            float tolerance = MathHelper.Lerp(2.5f, 10.0f, degreeOfSuccess);
            optimalTurbineOutput = new Vector2(correctTurbineOutput - tolerance, correctTurbineOutput + tolerance);
            tolerance = MathHelper.Lerp(5.0f, 20.0f, degreeOfSuccess);
            allowedTurbineOutput = new Vector2(correctTurbineOutput - tolerance, correctTurbineOutput + tolerance);
            
            float temperatureTolerance = MathHelper.Lerp(10.0f, 20.0f, degreeOfSuccess);
            optimalTemperature = Vector2.Lerp(new Vector2(40.0f, 60.0f), new Vector2(30.0f, 70.0f), degreeOfSuccess);
            allowedTemperature = Vector2.Lerp(new Vector2(30.0f, 70.0f), new Vector2(10.0f, 90.0f), degreeOfSuccess);

            float fissionRateTolerance = MathHelper.Lerp(10.0f, 20.0f, degreeOfSuccess);
            optimalFissionRate = Vector2.Lerp(new Vector2(40.0f, 70.0f), new Vector2(30.0f, 85.0f), degreeOfSuccess);
            allowedFissionRate = Vector2.Lerp(new Vector2(30.0f, 85.0f), new Vector2(20.0f, 98.0f), degreeOfSuccess);

            float heatAmount = fissionRate * (AvailableFuel / 100.0f) * 2.0f;
            float temperatureDiff = (heatAmount - turbineOutput) - Temperature;
            Temperature += MathHelper.Clamp(Math.Sign(temperatureDiff) * 10.0f * deltaTime, -Math.Abs(temperatureDiff), Math.Abs(temperatureDiff));
            if (item.InWater && AvailableFuel < 100.0f) Temperature -= 12.0f * deltaTime;

            FissionRate = MathHelper.Lerp(fissionRate, Math.Min(targetFissionRate, AvailableFuel), deltaTime);
            TurbineOutput = MathHelper.Lerp(turbineOutput, targetTurbineOutput, deltaTime);

            float temperatureFactor = Math.Min(temperature / 50.0f, 1.0f);
            currPowerConsumption = -MaxPowerOutput * Math.Min(turbineOutput / 100.0f, temperatureFactor);

            //if the turbine output and coolant flow are the optimal range, 
            //make the generated power slightly adjust according to the load
            //  (-> the reactor can automatically handle small changes in load as long as the values are roughly correct)
            if (turbineOutput > optimalTurbineOutput.X && turbineOutput < optimalTurbineOutput.Y && 
                temperature > optimalTemperature.X && temperature < optimalTemperature.Y)
            {
                float maxAutoAdjust = maxPowerOutput * 0.1f;
                autoAdjustAmount = MathHelper.Lerp(
                    autoAdjustAmount, 
                    MathHelper.Clamp(-load - currPowerConsumption, -maxAutoAdjust, maxAutoAdjust), 
                    deltaTime * 10.0f);
            }
            else
            {
                autoAdjustAmount = MathHelper.Lerp(autoAdjustAmount, 0.0f, deltaTime * 10.0f);
            }
            currPowerConsumption += autoAdjustAmount;

            if (shutDown)
            {
                targetFissionRate = 0.0f;
                targetTurbineOutput = 0.0f;
            }
            else if (autoTemp)
            {
                UpdateAutoTemp(2.0f, deltaTime);
            }

            load = 0.0f;
            List<Connection> connections = item.Connections;
            if (connections != null && connections.Count > 0)
            {
                foreach (Connection connection in connections)
                {
                    if (!connection.IsPower) continue;
                    foreach (Connection recipient in connection.Recipients)
                    {
                        Item it = recipient.Item as Item;
                        if (it == null) continue;

                        PowerTransfer pt = it.GetComponent<PowerTransfer>();
                        if (pt == null) continue;

                        load = Math.Max(load, pt.PowerLoad);
                    }
                }
            }

            if (fissionRate > 0.0f)
            {
                foreach (Item item in item.ContainedItems)
                {
                    if (!item.HasTag("reactorfuel")) continue;
                    item.Condition -= fissionRate / 100.0f * fuelConsumptionRate * deltaTime;
                }
            }

            item.SendSignal(0, ((int)(temperature * 100.0f)).ToString(), "temperature_out", null);

            UpdateFailures(deltaTime);
#if CLIENT
            UpdateGraph(deltaTime);
#endif
            AvailableFuel = 0.0f;

            sendUpdateTimer = Math.Max(sendUpdateTimer - deltaTime, 0.0f);

            if (unsentChanges && sendUpdateTimer <= 0.0f)
            {
                if (GameMain.Server != null)
                {
                    item.CreateServerEvent(this);
                }
#if CLIENT
                else if (GameMain.Client != null)
                {
                    item.CreateClientEvent(this);
                }
#endif
                sendUpdateTimer = NetworkUpdateInterval;
                unsentChanges = false;
            }
        }

        private void UpdateFailures(float deltaTime)
        {
            if (temperature > allowedTemperature.Y)
            {
                item.SendSignal(0, "1", "meltdown_warning", null);
                //faster meltdown if the item is in a bad condition
                meltDownTimer += MathHelper.Lerp(deltaTime * 2.0f, deltaTime, item.Condition / 100.0f);

                if (meltDownTimer > MeltdownDelay)
                {
                    MeltDown();
                    return;
                }
            }
            else
            {
                item.SendSignal(0, "0", "meltdown_warning", null);
                meltDownTimer = Math.Max(0.0f, meltDownTimer - deltaTime);
            }

            if (temperature > optimalTemperature.Y)
            {
                float prevFireTimer = fireTimer;
                fireTimer += MathHelper.Lerp(deltaTime * 2.0f, deltaTime, item.Condition / 100.0f);

                if (fireTimer >= FireDelay && prevFireTimer < fireDelay)
                {
                    new FireSource(item.WorldPosition);
                }
            }
            else
            {
                fireTimer = Math.Max(0.0f, fireTimer - deltaTime);
            }
        }

        private void UpdateAutoTemp(float speed, float deltaTime)
        {
            float desiredTurbineOutput = (optimalTurbineOutput.X + optimalTurbineOutput.Y) / 2.0f;
            targetTurbineOutput += MathHelper.Clamp(desiredTurbineOutput - targetTurbineOutput, -speed, speed) * deltaTime;

            float desiredFissionRate = (optimalFissionRate.X + optimalFissionRate.Y) / 2.0f;
            targetFissionRate += MathHelper.Clamp(desiredFissionRate - targetFissionRate, -speed, speed) * deltaTime;

            if (temperature > (optimalTemperature.X + optimalTemperature.Y) / 2.0f)
            {
                targetFissionRate = Math.Min(targetFissionRate - speed * 2 * deltaTime, allowedFissionRate.Y);
            }
            else if (-currPowerConsumption < load)
            {
                targetFissionRate = Math.Min(targetFissionRate + speed * 2 * deltaTime, allowedFissionRate.Y);
            }
        }
        
        public override void UpdateBroken(float deltaTime, Camera cam)
        {
            base.UpdateBroken(deltaTime, cam);

            currPowerConsumption = 0.0f;
            Temperature -= deltaTime * 1000.0f;
            targetFissionRate = Math.Max(targetFissionRate - deltaTime * 10.0f, 0.0f);
            targetTurbineOutput = Math.Max(targetTurbineOutput - deltaTime * 10.0f, 0.0f);
#if CLIENT
            fissionRateScrollBar.BarScroll = 1.0f - FissionRate / 100.0f;
            turbineOutputScrollBar.BarScroll = 1.0f - TurbineOutput / 100.0f;
            UpdateGraph(deltaTime);
#endif
        }

        private void MeltDown()
        {
            if (item.Condition <= 0.0f || GameMain.Client != null) return;

            GameServer.Log("Reactor meltdown!", ServerLog.MessageType.ItemInteraction);

            item.Condition = 0.0f;
            fireTimer = 0.0f;
            meltDownTimer = 0.0f;

            var containedItems = item.ContainedItems;
            if (containedItems != null)
            {
                foreach (Item containedItem in containedItems)
                {
                    if (containedItem == null) continue;
                    containedItem.Condition = 0.0f;
                }
            }
            
            if (GameMain.Server != null && GameMain.Server.ConnectedClients.Contains(BlameOnBroken))
            {
                BlameOnBroken.Karma = 0.0f;
            }            
        }

        public override bool Pick(Character picker)
        {
            return picker != null;
        }

        public override bool AIOperate(float deltaTime, Character character, AIObjectiveOperateItem objective)
        {
            if (GameMain.Client != null) return false;

            float degreeOfSuccess = DegreeOfSuccess(character);

            //characters with insufficient skill levels don't refuel the reactor
            if (degreeOfSuccess > 0.2f)
            {
                //remove used-up fuel from the reactor
                var containedItems = item.ContainedItems;
                foreach (Item item in containedItems)
                {
                    if (item != null && item.Condition <= 0.0f)
                    {
                        item.Drop();
                    }
                }

                //we need more fuel
                if (-currPowerConsumption < load * 0.5f && prevAvailableFuel <= 0.0f)
                {
                    var containFuelObjective = new AIObjectiveContainItem(character, new string[] { "fuelrod", "reactorfuel" }, item.GetComponent<ItemContainer>());
                    containFuelObjective.MinContainedAmount = containedItems.Count(i => i != null && i.Prefab.Identifier == "fuelrod" || i.HasTag("reactorfuel")) + 1;
                    containFuelObjective.GetItemPriority = (Item fuelItem) =>
                    {
                        if (fuelItem.ParentInventory?.Owner is Item)
                        {
                            //don't take fuel from other reactors
                            if (((Item)fuelItem.ParentInventory.Owner).GetComponent<Reactor>() != null) return 0.0f;
                        }
                        return 1.0f;
                    };
                    objective.AddSubObjective(containFuelObjective);

                    character?.Speak(TextManager.Get("DialogReactorFuel"), null, 0.0f, "reactorfuel", 30.0f);

                    return false;
                }
            }

            if (aiUpdateTimer > 0.0f)
            {
                aiUpdateTimer -= deltaTime;
                return false;
            }

            if (lastUser != character && lastUser != null && lastUser.SelectedConstruction == item)
            {
                character.Speak(TextManager.Get("DialogReactorTaken"), null, 0.0f, "reactortaken", 10.0f);
            }

            LastUser = character;
            
            switch (objective.Option.ToLowerInvariant())
            {
                case "powerup":
#if CLIENT
                    onOffSwitch.BarScroll = 0.0f;
#endif
                    shutDown = false;
                    //characters with insufficient skill levels simply set the autotemp on instead of trying to adjust the temperature manually
                    if (degreeOfSuccess < 0.5f)
                    {
                        if (!autoTemp) unsentChanges = true;
                        AutoTemp = true;
                    }
                    else
                    {
                        AutoTemp = false;
                        unsentChanges = true;
                        UpdateAutoTemp(2.0f + degreeOfSuccess * 5.0f, 1.0f);
                    }                    
                    break;
                case "shutdown":
#if CLIENT
                    onOffSwitch.BarScroll = 1.0f;
#endif
                    AutoTemp = false;
                    shutDown = true;
                    targetFissionRate = 0.0f;
                    targetTurbineOutput = 0.0f;
                    break;
            }

            aiUpdateTimer = AIUpdateInterval;

            return false;
        }

        public override void ReceiveSignal(int stepsTaken, string signal, Connection connection, Item source, Character sender, float power, float signalStrength = 1.0f)
        {
            switch (connection.Name)
            {
                case "shutdown":
                    if (targetFissionRate > 0.0f || targetTurbineOutput > 0.0f)
                    {
                        shutDown = true;
                        AutoTemp = false;
                        targetFissionRate = 0.0f;
                        targetTurbineOutput = 0.0f;
                        unsentChanges = true;
#if CLIENT
                        onOffSwitch.BarScroll = 1.0f;
#endif
                    }
                    break;
            }
        }

        public void ServerRead(ClientNetObject type, NetBuffer msg, Client c)
        {
            bool autoTemp       = msg.ReadBoolean();
            bool shutDown       = msg.ReadBoolean();
            float fissionRate   = msg.ReadRangedSingle(0.0f, 100.0f, 8);
            float turbineOutput = msg.ReadRangedSingle(0.0f, 100.0f, 8);

            if (!item.CanClientAccess(c)) return;

            if (!autoTemp && AutoTemp) BlameOnBroken = c;
            if (turbineOutput < targetTurbineOutput) BlameOnBroken = c;
            if (fissionRate > targetFissionRate) BlameOnBroken = c;
            if (!this.shutDown && shutDown) BlameOnBroken = c;
            
            AutoTemp = autoTemp;
            this.shutDown = shutDown;
            targetFissionRate = fissionRate;
            targetTurbineOutput = turbineOutput;

            LastUser = c.Character;
            if (nextServerLogWriteTime == null)
            {
                nextServerLogWriteTime = Math.Max(lastServerLogWriteTime + 1.0f, (float)Timing.TotalTime);
            }

#if CLIENT
            fissionRateScrollBar.BarScroll = 1.0f - targetFissionRate / 100.0f;
            turbineOutputScrollBar.BarScroll = 1.0f - targetTurbineOutput / 100.0f;
            onOffSwitch.BarScroll = shutDown ? Math.Max(onOffSwitch.BarScroll, 0.55f) : Math.Min(onOffSwitch.BarScroll, 0.45f);
#endif

            //need to create a server event to notify all clients of the changed state
            unsentChanges = true;
        }

        public void ServerWrite(NetBuffer msg, Client c, object[] extraData = null)
        {
            msg.Write(autoTemp);
            msg.Write(shutDown);
            msg.WriteRangedSingle(temperature, 0.0f, 100.0f, 8);
            msg.WriteRangedSingle(targetFissionRate, 0.0f, 100.0f, 8);
            msg.WriteRangedSingle(targetTurbineOutput, 0.0f, 100.0f, 8);
            msg.WriteRangedSingle(degreeOfSuccess, 0.0f, 1.0f, 8);
        }
    }
}
