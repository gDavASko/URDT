using System;
using KBP.CORE;
using UnityEngine;
using UnityEngine.Scripting;

namespace KBP.CORE.MODULES
{
    /// <summary>
    /// Custom transition condition implementing IModuleTransitionCondition.
    /// Evaluates if player inventory context contains the specified item code.
    /// </summary>
    [Serializable]
    [Preserve]
    public sealed class HasItemTransitionCondition : IModuleTransitionCondition
    {
        [SerializeField, ConstSelector("Items")] private string _itemId = string.Empty;

        public string ConditionId => "HasItem";

        public bool Evaluate(Parameters context)
        {
            if (context.TryGet("owned_items", out string itemsString))
            {
                if (!string.IsNullOrEmpty(itemsString))
                {
                    return itemsString.Contains(_itemId);
                }
            }
            return false;
        }
    }
}
