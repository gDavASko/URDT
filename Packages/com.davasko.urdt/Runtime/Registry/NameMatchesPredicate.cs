using System.Text.RegularExpressions;
using UnityEngine;

namespace KBP.URDT.Registry
{
    /// <summary>
    /// Matches the object's name against a simple glob pattern (<c>*</c> wildcard).
    /// The pattern is compiled to a <see cref="Regex"/> once at construction, so
    /// per-registration matching allocates nothing beyond the match call.
    /// Stable trait (04 §2.4).
    /// </summary>
    public sealed class NameMatchesPredicate : ISelectorPredicate
    {
        private readonly Regex _regex;

        public NameMatchesPredicate(string globPattern)
        {
            string escaped = Regex.Escape(globPattern ?? string.Empty).Replace("\\*", ".*");
            _regex = new Regex("^" + escaped + "$", RegexOptions.CultureInvariant);
        }

        public bool Matches(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            return _regex.IsMatch(gameObject.name);
        }
    }
}
