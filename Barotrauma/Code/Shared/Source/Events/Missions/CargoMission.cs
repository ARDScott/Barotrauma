﻿using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma
{
    class CargoMission : Mission
    {
        private XElement itemConfig;

        private List<Item> items;

        private int requiredDeliveryAmount;

        public CargoMission(MissionPrefab prefab, Location[] locations)
            : base(prefab, locations)
        {
            itemConfig = prefab.ConfigElement.Element("Items");
            requiredDeliveryAmount = prefab.ConfigElement.GetAttributeInt("requireddeliveryamount", 0);
        }

        private void InitItems()
        {
            items = new List<Item>();

            if (itemConfig == null)
            {
                DebugConsole.ThrowError("Failed to initialize items for cargo mission (itemConfig == null)");
                return;
            }

            foreach (XElement subElement in itemConfig.Elements())
            {
                LoadItemAsChild(subElement, null);
            }

            if (requiredDeliveryAmount == 0) requiredDeliveryAmount = items.Count;
        }

        private void LoadItemAsChild(XElement element, Item parent)
        {
            ItemPrefab itemPrefab;
            if (element.Attribute("name") != null)
            {
                DebugConsole.ThrowError("Error in cargo mission \"" + Name + "\" - use item identifiers instead of names to configure the items.");
                string itemName = element.GetAttributeString("name", "");
                itemPrefab = MapEntityPrefab.Find(itemName) as ItemPrefab;
                if (itemPrefab == null)
                {
                    DebugConsole.ThrowError("Couldn't spawn item for cargo mission: item prefab \"" + itemName + "\" not found");
                    return;
                }
            }
            else
            {
                string itemIdentifier = element.GetAttributeString("identifier", "");
                itemPrefab = MapEntityPrefab.Find(null, itemIdentifier) as ItemPrefab;
                if (itemPrefab == null)
                {
                    DebugConsole.ThrowError("Couldn't spawn item for cargo mission: item prefab \"" + itemIdentifier + "\" not found");
                    return;
                }
            }

            if (itemPrefab == null)
            {
                DebugConsole.ThrowError("Couldn't spawn item for cargo mission: item prefab \"" + element.Name.ToString() + "\" not found");
                return;
            }

            WayPoint cargoSpawnPos = WayPoint.GetRandom(SpawnType.Cargo, null, Submarine.MainSub, true);
            if (cargoSpawnPos == null)
            {
                DebugConsole.ThrowError("Couldn't spawn items for cargo mission, cargo spawnpoint not found");
                return;
            }

            var cargoRoom = cargoSpawnPos.CurrentHull;

            if (cargoRoom == null)
            {
                DebugConsole.ThrowError("A waypoint marked as Cargo must be placed inside a room!");
                return;
            }

            Vector2 position = new Vector2(
                cargoSpawnPos.Position.X + Rand.Range(-20.0f, 20.0f, Rand.RandSync.Server),
                cargoRoom.Rect.Y - cargoRoom.Rect.Height + itemPrefab.Size.Y / 2);

            var item = new Item(itemPrefab, position, cargoRoom.Submarine);
            item.FindHull();


            items.Add(item);
            
            if (parent != null) parent.Combine(item);
            
            foreach (XElement subElement in element.Elements())
            {
                int amount = subElement.GetAttributeInt("amount", 1);
                for (int i = 0; i < amount; i++)
                {
                    LoadItemAsChild(subElement, item);
                }                    
            }
        }

        public override void Start(Level level)
        {
            InitItems();
        }

        public override void End()
        {
            if (Submarine.MainSub != null && Submarine.MainSub.AtEndPosition)
            {
                int deliveredItemCount = items.Count(i => i.CurrentHull != null && !i.Removed && i.Condition > 0.0f);

                if (deliveredItemCount >= requiredDeliveryAmount)
                {
                    GiveReward();

                    completed = true;
                }
            }

            foreach (Item item in items)
            {
                if (!item.Removed) item.Remove();
            }
        }
    }
}
