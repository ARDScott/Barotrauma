﻿using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma
{
    [Flags]
    public enum InvSlotType
    {
        None = 0, Any = 1, RightHand = 2, LeftHand = 4, Head = 8, InnerClothes = 16, OuterClothes = 32, Headset = 64, Card = 128
    };

    partial class CharacterInventory : Inventory
    {
        private Character character;

        public InvSlotType[] SlotTypes
        {
            get;
            private set;
        }

        protected bool[] IsEquipped;

        public bool AccessibleWhenAlive
        {
            get;
            private set;
        }

        public CharacterInventory(XElement element, Character character)
            : base(character, element.GetAttributeString("slots", "").Split(',').Count())
        {
            this.character = character;
            IsEquipped = new bool[capacity];
            SlotTypes = new InvSlotType[capacity];

            AccessibleWhenAlive = element.GetAttributeBool("accessiblewhenalive", true);

            string[] slotTypeNames = element.GetAttributeString("slots", "").Split(',');
            System.Diagnostics.Debug.Assert(slotTypeNames.Length == capacity);

            for (int i = 0; i < capacity; i++)
            {
                InvSlotType parsedSlotType = InvSlotType.Any;
                slotTypeNames[i] = slotTypeNames[i].Trim();
                if (!Enum.TryParse(slotTypeNames[i], out parsedSlotType))
                {
                    DebugConsole.ThrowError("Error in the inventory config of \"" + character.SpeciesName + "\" - " + slotTypeNames[i] + " is not a valid inventory slot type.");
                }
                SlotTypes[i] = parsedSlotType;
                switch (SlotTypes[i])
                {
                    //case InvSlotType.Head:
                    case InvSlotType.OuterClothes:
                    case InvSlotType.LeftHand:
                    case InvSlotType.RightHand:
                        hideEmptySlot[i] = true;
                        break;
                }               
            }
            
            InitProjSpecific(element);

            //clients don't create items until the server says so
            if (GameMain.Client != null) return;

            foreach (XElement subElement in element.Elements())
            {
                if (subElement.Name.ToString().ToLowerInvariant() != "item") continue;
                
                string itemIdentifier = subElement.GetAttributeString("identifier", "");
                ItemPrefab itemPrefab = MapEntityPrefab.Find(null, itemIdentifier) as ItemPrefab;
                if (itemPrefab == null)
                {
                    DebugConsole.ThrowError("Error in character inventory \"" + character.SpeciesName + "\" - item \"" + itemIdentifier + "\" not found.");
                    continue;
                }

                Entity.Spawner?.AddToSpawnQueue(itemPrefab, this);
            }
        }

        partial void InitProjSpecific(XElement element);

        private bool UseItemOnSelf(int slotIndex)
        {
            if (Items[slotIndex] == null) return false;

#if CLIENT
            if (GameMain.Client != null)
            {
                GameMain.Client.CreateEntityEvent(Items[slotIndex], new object[] { NetEntityEvent.Type.ApplyStatusEffect });
                return true;
            }
#endif

            if (GameMain.Server != null)
            {
                GameMain.Server.CreateEntityEvent(Items[slotIndex], new object[] { NetEntityEvent.Type.ApplyStatusEffect, ActionType.OnUse, character.ID });
            }

            Items[slotIndex].ApplyStatusEffects(ActionType.OnUse, 1.0f, character);

            //item may have been removed by a status effect
            if (Items[slotIndex] == null) return true;

            foreach (ItemComponent ic in Items[slotIndex].components)
            {
                if (ic.DeleteOnUse)
                {
                    Entity.Spawner.AddToRemoveQueue(Items[slotIndex]);
                }
            }
            
            return true;
        }
        
        public int FindLimbSlot(InvSlotType limbSlot)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (SlotTypes[i] == limbSlot) return i;
            }
            return -1;
        }

        public bool IsInLimbSlot(Item item, InvSlotType limbSlot)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i] == item && SlotTypes[i] == limbSlot) return true;
            }
            return false;
        }

        public override bool CanBePut(Item item, int i)
        {
            return base.CanBePut(item, i) && item.AllowedSlots.Contains(SlotTypes[i]);
        } 

        /// <summary>
        /// If there is room, puts the item in the inventory and returns true, otherwise returns false
        /// </summary>
        public override bool TryPutItem(Item item, Character user, List<InvSlotType> allowedSlots = null, bool createNetworkEvent = true)
        {
            if (allowedSlots == null || !allowedSlots.Any()) return false;

            bool inSuitableSlot = false;
            bool inWrongSlot = false;
            int currentSlot = -1;
            for (int i = 0; i < capacity; i++)
            {
                if (Items[i] == item)
                {
                    currentSlot = i;
                    if (allowedSlots.Any(a => a.HasFlag(SlotTypes[i])))
                        inSuitableSlot = true;
                    else if (!allowedSlots.Any(a => a.HasFlag(SlotTypes[i])))
                        inWrongSlot = true;
                }
            }
            //all good
            if (inSuitableSlot && !inWrongSlot) return true;

            //try to place the item in a LimbSlot.Any slot if that's allowed
            if (allowedSlots.Contains(InvSlotType.Any))
            {
                for (int i = 0; i < capacity; i++)
                {
                    if (SlotTypes[i] != InvSlotType.Any) continue;
                    if (Items[i] == item)
                    {
                        PutItem(item, i, user, true, createNetworkEvent);
                        item.Unequip(character);
                        return true;
                    }
                }
                for (int i = 0; i < capacity; i++)
                {
                    if (SlotTypes[i] != InvSlotType.Any) continue;
                    if (inWrongSlot)
                    {
                        if (Items[i] != item && Items[i] != null) continue;
                    }
                    else
                    {
                        if (Items[i] != null) continue;
                    }

                    PutItem(item, i, user, true, createNetworkEvent);
                    item.Unequip(character);
                    return true;
                }
            }

            int placedInSlot = -1;
            foreach (InvSlotType allowedSlot in allowedSlots)
            {
                //check if all the required slots are free
                bool free = true;
                for (int i = 0; i < capacity; i++)
                {
                    if (allowedSlot.HasFlag(SlotTypes[i]) && Items[i] != null && Items[i] != item)
                    {
                        free = false;
#if CLIENT
                        for (int j = 0; j < capacity; j++)
			            {
                            if (slots != null && Items[j] == Items[i]) slots[j].ShowBorderHighlight(Color.Red, 0.1f, 0.9f);
			            }
#endif
                    }
                }

                if (!free) continue;

                for (int i = 0; i < capacity; i++)
                {
                    if (allowedSlot.HasFlag(SlotTypes[i]) && Items[i] == null)
                    {
                        bool removeFromOtherSlots = item.ParentInventory != this;
                        if (placedInSlot == -1 && inWrongSlot)
                        {
                            if (!hideEmptySlot[i] || SlotTypes[currentSlot] != InvSlotType.Any) removeFromOtherSlots = true;
                        }

                        PutItem(item, i, user, removeFromOtherSlots, createNetworkEvent);
                        item.Equip(character);
                        placedInSlot = i;
                    }
                }

                if (placedInSlot > -1)
                {
                    if (item.AllowedSlots.Contains(InvSlotType.Any) && hideEmptySlot[placedInSlot])
                    {
                        bool isInAnySlot = false;
                        for (int i = 0; i < capacity; i++)
                        {
                            if (SlotTypes[i] == InvSlotType.Any && Items[i]==item)
                            {
                                isInAnySlot = true;
                                break;
                            }
                        }
                        if (!isInAnySlot)
                        {
                            for (int i = 0; i < capacity; i++)
                            {
                                if (SlotTypes[i] == InvSlotType.Any && Items[i] == null)
                                {
                                    Items[i] = item;
                                    break;
                                }
                            }
                        }
                    }
                    return true;
                }
            }


            return placedInSlot > -1;
        }

        public override bool TryPutItem(Item item, int index, bool allowSwapping, bool allowCombine, Character user, bool createNetworkEvent = true)
        {
            //there's already an item in the slot
            if (Items[index] != null)
            {
                if (Items[index] == item) return false;

                return base.TryPutItem(item, index, allowSwapping, allowCombine, user, createNetworkEvent);
            }

            if (SlotTypes[index] == InvSlotType.Any)
            {
                if (!item.AllowedSlots.Contains(InvSlotType.Any)) return false;
                if (Items[index] != null) return Items[index] == item;

                PutItem(item, index, user, true, createNetworkEvent);
                return true;
            }

            InvSlotType placeToSlots = InvSlotType.None;

            bool slotsFree = true;
            List<InvSlotType> allowedSlots = item.AllowedSlots;
            foreach (InvSlotType allowedSlot in allowedSlots)
            {
                if (!allowedSlot.HasFlag(SlotTypes[index])) continue;

                for (int i = 0; i < capacity; i++)
                {
                    if (allowedSlot.HasFlag(SlotTypes[i]) && Items[i] != null && Items[i] != item)
                    {
                        slotsFree = false;
                        break;
                    }

                    placeToSlots = allowedSlot;
                }
            }

            if (!slotsFree) return false;

            return TryPutItem(item, user, new List<InvSlotType>() { placeToSlots }, createNetworkEvent);
        }
    }
}
