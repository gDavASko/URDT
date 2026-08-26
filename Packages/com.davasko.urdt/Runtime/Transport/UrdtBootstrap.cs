namespace KBP.URDT.Transport
{
    /// <summary>
    /// Explicit bootstrap of the URDT dispatch layer. A host (dev/test build only —
    /// the whole assembly compiles under the <c>URDT_ENABLED</c> define) calls
    /// <see cref="Initialize"/> to create and configure the <c>DontDestroyOnLoad</c>
    /// <see cref="MainThreadDispatcher"/> singleton. Kept explicit (not auto-run) so
    /// nothing is injected into the running game until a host wires it up.
    /// </summary>
    public static class UrdtBootstrap
    {
        public static MainThreadDispatcher Initialize(CommandRouter router, int maxCommandsPerFrame = 16)
        {
            MainThreadDispatcher dispatcher = MainThreadDispatcher.EnsureInstance();
            dispatcher.Configure(router, maxCommandsPerFrame);
            return dispatcher;
        }
    }
}
