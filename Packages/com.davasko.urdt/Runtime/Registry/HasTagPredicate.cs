using UnityEngine;

namespace KBP.URDT.Registry
{
    /// <summary>
    /// Matches when the object's tag equals <see cref="_tag"/>. Stable trait (04 §2.4).
    /// </summary>
    public sealed class HasTagPredicate : ISelectorPredicate
    {
        private readonly string _tag;

        public HasTagPredicate(string tag)
        {
            _tag = tag;
        }

        public bool Matches(GameObject gameObject)
        {
            if (gameObject == null || string.IsNullOrEmpty(_tag))
            {
                return false;
            }

            return gameObject.CompareTag(_tag);
        }
    }
}
