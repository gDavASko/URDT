using UnityEngine;

namespace KBP.URDT.Registry
{
    /// <summary>
    /// Matches when ALL child predicates match (logical AND).
    /// </summary>
    public sealed class AllOfPredicate : ISelectorPredicate
    {
        private readonly ISelectorPredicate[] _predicates;

        public AllOfPredicate(params ISelectorPredicate[] predicates)
        {
            _predicates = predicates ?? new ISelectorPredicate[0];
        }

        public bool Matches(GameObject gameObject)
        {
            for (int i = 0; i < _predicates.Length; i++)
            {
                if (_predicates[i] == null || !_predicates[i].Matches(gameObject))
                {
                    return false;
                }
            }

            return _predicates.Length > 0;
        }
    }
}
