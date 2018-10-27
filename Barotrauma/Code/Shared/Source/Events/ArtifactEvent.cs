﻿using Microsoft.Xna.Framework;
using System;
using System.Xml.Linq;

namespace Barotrauma
{
    class ArtifactEvent : ScriptedEvent
    {
        private ItemPrefab itemPrefab;

        private Item item;

        private int state;

        private Vector2 spawnPos;

        private bool spawnPending;

        public override Vector2 DebugDrawPos
        {
            get { return spawnPos; }
        }

        public override string DebugDrawText
        {
            get { return "ArtifactEvent (" + itemPrefab.Name + ")"; }
        }

        public override string ToString()
        {
            return "ScriptedEvent (" + (itemPrefab == null ? "null" : itemPrefab.Name) + ")";
        }

        public ArtifactEvent(ScriptedEventPrefab prefab)
            : base(prefab)
        {
            if (prefab.ConfigElement.Attribute("itemname") != null)
            {
                DebugConsole.ThrowError("Error in ArtifactEvent - use item identifier instead of the name of the item.");
                string itemName = prefab.ConfigElement.GetAttributeString("itemname", "");
                itemPrefab = MapEntityPrefab.Find(itemName) as ItemPrefab;
                if (itemPrefab == null)
                {
                    DebugConsole.ThrowError("Error in SalvageMission: couldn't find an item prefab with the name " + itemName);
                }
            }
            else
            {
                string itemIdentifier = prefab.ConfigElement.GetAttributeString("itemidentifier", "");
                itemPrefab = MapEntityPrefab.Find(null, itemIdentifier) as ItemPrefab;
                if (itemPrefab == null)
                {
                    DebugConsole.ThrowError("Error in ArtifactEvent - couldn't find an item prefab with the identifier " + itemIdentifier);
                }
            }
        }

        public override void Init(bool affectSubImmediately)
        {
            spawnPos = Level.Loaded.GetRandomItemPos(
                (Rand.Range(0.0f, 1.0f, Rand.RandSync.Server) < 0.5f) ? Level.PositionType.MainPath : Level.PositionType.Cave | Level.PositionType.Ruin,
                500.0f, 10000.0f, 30.0f);

            spawnPending = true;
        }
        
        private void SpawnItem()
        {
            item = new Item(itemPrefab, spawnPos, null);
            item.body.FarseerBody.IsKinematic = true;

            //try to find a nearby artifact holder (or any alien itemcontainer) and place the artifact inside it
            foreach (Item it in Item.ItemList)
            {
                if (it.Submarine != null || !it.HasTag("alien")) continue;

                if (Math.Abs(item.WorldPosition.X - it.WorldPosition.X) > 2000.0f) continue;
                if (Math.Abs(item.WorldPosition.Y - it.WorldPosition.Y) > 2000.0f) continue;

                var itemContainer = it.GetComponent<Items.Components.ItemContainer>();
                if (itemContainer == null) continue;

                itemContainer.Combine(item);
                break;
            }

            if (GameSettings.VerboseLogging)
            {
                DebugConsole.NewMessage("Initialized ArtifactEvent (" + item.Name + ")", Color.White);
            }

            if (GameMain.Server != null)
            {
                Entity.Spawner.CreateNetworkEvent(item, false);
            }
        }

        public override void Update(float deltaTime)
        {
            if (spawnPending)
            {
                SpawnItem();
                spawnPending = false;
            }

            switch (state)
            {
                case 0:
                    if (item.ParentInventory != null) item.body.FarseerBody.IsKinematic = false;                    
                    if (item.CurrentHull == null) return;

                    state = 1;
                    break;
                case 1:
                    if (!Submarine.MainSub.AtEndPosition && !Submarine.MainSub.AtStartPosition) return;

                    Finished();
                    state = 2;
                    break;
            }    
        }
    }
}
