using KBP.URDT;
using KBP.URDT.Inspect;
using KBP.URDT.Registry;
using UnityEngine;

namespace KBP.URDT.TestPoligon
{
    /// <summary>
    /// Scene-local bootstrap for autonomous URDT polygon runs.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-31990)]
    public sealed class UrdtTestPoligonBootstrap : MonoBehaviour
    {
        private const string TEST_ID_TEMPLATE = "urdt_ui";
        private const int DEFAULT_PORT = 7777;
        private const string DEFAULT_TOKEN = "urdt-test-poligon";

        [SerializeField] private UrdtServerHost _serverHost = null;
        [SerializeField] private bool _startServerOnStart = true;
        [SerializeField] private int _port = DEFAULT_PORT;
        [SerializeField] private string _token = DEFAULT_TOKEN;

        public UrdtServerHost ServerHost
        {
            get { return _serverHost; }
        }

        public void Configure(UrdtServerHost serverHost, bool startServerOnStart, int port, string token)
        {
            _serverHost = serverHost;
            _startServerOnStart = startServerOnStart;
            _port = port;
            _token = string.IsNullOrEmpty(token) ? DEFAULT_TOKEN : token;
        }

        private void Awake()
        {
            if (_serverHost == null)
            {
                TryGetComponent(out _serverHost);
            }

            if (_serverHost != null)
            {
                _serverHost.RegisterSelectorRule(new HasComponentPredicate(nameof(UrdtDebugTarget)), TEST_ID_TEMPLATE);
                _serverHost.RegisterSelectorRule(new HasComponentPredicate(nameof(UrdtUiTarget)), TEST_ID_TEMPLATE);
                _serverHost.RegisterSelectorRule(new HasComponentPredicate(nameof(UrdtUiButtonTarget)), TEST_ID_TEMPLATE);
                _serverHost.RegisterSelectorRule(new HasComponentPredicate(nameof(UrdtUiToggleTarget)), TEST_ID_TEMPLATE);
                _serverHost.RegisterSelectorRule(new HasComponentPredicate(nameof(UrdtUiSliderTarget)), TEST_ID_TEMPLATE);
                _serverHost.RegisterSelectorRule(new HasComponentPredicate(nameof(UrdtUiInputTarget)), TEST_ID_TEMPLATE);
                _serverHost.RegisterSelectorRule(new HasComponentPredicate(nameof(UrdtUiDropdownTarget)), TEST_ID_TEMPLATE);
                _serverHost.RegisterSelectorRule(new HasComponentPredicate(nameof(UrdtUiScrollTarget)), TEST_ID_TEMPLATE);
                _serverHost.RegisterSelectorRule(new HasComponentPredicate(nameof(UrdtUiWindowTarget)), TEST_ID_TEMPLATE);
                _serverHost.RegisterSelectorRule(new HasComponentPredicate(nameof(UrdtUiGenericTarget)), TEST_ID_TEMPLATE);
            }
        }

        private void Start()
        {
            if (_startServerOnStart && _serverHost != null && !_serverHost.IsRunning)
            {
                _serverHost.StartServer(_port, _token);
            }
        }
    }
}
