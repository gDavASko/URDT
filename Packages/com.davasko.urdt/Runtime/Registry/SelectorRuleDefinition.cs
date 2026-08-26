using System;
using UnityEngine;

namespace KBP.URDT.Registry
{
    /// <summary>
    /// Inspector-friendly selector rule definition used by <see cref="UrdtServerHost"/>
    /// to populate the runtime TestId registry on startup.
    /// </summary>
    [Serializable]
    public sealed class SelectorRuleDefinition
    {
        [SerializeField] private SelectorRuleKind _kind = SelectorRuleKind.HasComponent;
        [SerializeField] private string _match = null;
        [SerializeField] private string _testIdTemplate = null;

        public bool TryBuild(out SelectorRule rule)
        {
            rule = null;
            if (string.IsNullOrEmpty(_match) || string.IsNullOrEmpty(_testIdTemplate))
            {
                return false;
            }

            ISelectorPredicate predicate;
            switch (_kind)
            {
                case SelectorRuleKind.HasTag:
                    predicate = new HasTagPredicate(_match);
                    break;
                case SelectorRuleKind.NameMatches:
                    predicate = new NameMatchesPredicate(_match);
                    break;
                default:
                    predicate = new HasComponentPredicate(_match);
                    break;
            }

            rule = new SelectorRule(predicate, _testIdTemplate);
            return true;
        }
    }

    public enum SelectorRuleKind
    {
        HasComponent = 0,
        HasTag = 1,
        NameMatches = 2
    }
}
