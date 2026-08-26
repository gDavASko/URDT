using UnityEngine;

namespace KBP.URDT.Registry
{
    /// <summary>
    /// Matches when the inner predicate does NOT match (logical NOT).
    /// <para>
    /// Caution (04 §2.4): a rule negating a MUTABLE-state component (e.g.
    /// <c>not hasComponent: Healed</c>) is an anti-pattern — TestId is not
    /// re-evaluated between registration points, so it would go stale. Use NOT only
    /// over stable traits.
    /// </para>
    /// </summary>
    public sealed class NotPredicate : ISelectorPredicate
    {
        private readonly ISelectorPredicate _inner;

        public NotPredicate(ISelectorPredicate inner)
        {
            _inner = inner;
        }

        public bool Matches(GameObject gameObject)
        {
            if (_inner == null)
            {
                return false;
            }

            return !_inner.Matches(gameObject);
        }
    }
}
