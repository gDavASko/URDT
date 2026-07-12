using UnityEngine;

namespace KBP.URDT.Registry
{
    /// <summary>
    /// Predicate of a Selector Rule. Evaluated ONLY at registration points
    /// (scene scan, incremental hook, lazy resolve) and only against STABLE traits
    /// (component type, tag, name) — never on mutable state (04 §2.4).
    /// </summary>
    public interface ISelectorPredicate
    {
        bool Matches(GameObject gameObject);
    }
}
