﻿using Barotrauma.Items.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace Barotrauma
{
    class AIObjectiveRepairItems : AIObjective
    {
        public AIObjectiveRepairItems(Character character)
            : base(character, "")
        {
        }

        public override float GetPriority(AIObjectiveManager objectiveManager)
        {
            GetBrokenItems();
            if (subObjectives.Count > 0 && objectiveManager.CurrentOrder == this)
            {
                return AIObjectiveManager.OrderPriority;
            }

            return 1.0f;
        }
                
        public override bool IsCompleted()
        {
            return false;
        }

        public override bool IsDuplicate(AIObjective otherObjective)
        {
            return otherObjective is AIObjectiveRepairItems;
        }

        protected override void Act(float deltaTime)
        {
            GetBrokenItems();
        }

        private void GetBrokenItems()
        {
            foreach (Item item in Item.ItemList)
            {
                //ignore items that are in full condition
                if (item.Condition >= 100.0f) continue;
                foreach (Repairable repairable in item.Repairables)
                {
                    //ignore ones that are already fixed
                    if (repairable.Fixed || item.Condition > repairable.ShowRepairUIThreshold) continue;

                    AddSubObjective(new AIObjectiveRepairItem(character, item));
                    break;
                }
            }
        }
    }
}
