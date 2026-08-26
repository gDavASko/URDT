using UnityEngine;

namespace KBP.URDT.Registry
{
    /// <summary>
    /// Matches when the object carries a component whose type name equals
    /// <see cref="_componentTypeName"/>. Stable trait (04 §2.4).
    /// </summary>
    public sealed class HasComponentPredicate : ISelectorPredicate
    {
        private readonly string _componentTypeName;

        public HasComponentPredicate(string componentTypeName)
        {
            _componentTypeName = componentTypeName;
        }

        public bool Matches(GameObject gameObject)
        {
            if (gameObject == null || string.IsNullOrEmpty(_componentTypeName))
            {
                return false;
            }

            return gameObject.GetComponent(_componentTypeName) != null;
        }
    }
}
