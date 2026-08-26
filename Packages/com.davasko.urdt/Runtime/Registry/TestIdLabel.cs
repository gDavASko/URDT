using KBP.URDT.Driver;
using UnityEngine;

namespace KBP.URDT.Registry
{
    /// <summary>
    /// Runtime-only label attached by <see cref="TestIdRegistry"/> to a registered
    /// object (invariant I5 / FR-11). It is created ONLY in Play Mode and is flagged
    /// <see cref="HideFlags.DontSave"/> so it is never serialized into scenes or
    /// prefabs. It bridges the object's <c>OnEnable</c>/<c>OnDisable</c>/<c>OnDestroy</c>
    /// to the registry so the label's lifetime equals the object's lifetime
    /// (pool reuse re-registers with a fresh TestId, 04 §2.2/§4.1).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TestIdLabel : MonoBehaviour
    {
        private TestIdRegistry _registry;
        private string _testId;
        private Handle _handle;

        public string TestId
        {
            get { return _testId; }
        }

        public Handle Handle
        {
            get { return _handle; }
        }

        public void Bind(TestIdRegistry registry, string testId, Handle handle)
        {
            _registry = registry;
            _testId = testId;
            _handle = handle;
            hideFlags = HideFlags.DontSave;
        }

        private void OnEnable()
        {
            if (_registry != null)
            {
                _registry.NotifyLabelEnabled(this);
            }
        }

        private void OnDisable()
        {
            if (_registry != null)
            {
                _registry.NotifyLabelDisabled(this);
            }
        }

        private void OnDestroy()
        {
            if (_registry != null)
            {
                _registry.NotifyLabelDestroyed(this);
            }
        }
    }
}
