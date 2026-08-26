namespace KBP.URDT.Lifecycle
{
    /// <summary>
    /// Optional game-provided hook for <c>reset_state depth:"services"</c> (TZ §8.7): clears
    /// service/runtime state that a scene reload alone does not reset (DontDestroyOnLoad
    /// singletons, static fields, EventBus subscriptions, DataService runtime data, ...).
    /// URDT stays engine-agnostic — the game implements and registers this; if absent, a
    /// <c>services</c> reset is reported <c>E_UNSUPPORTED</c>.
    /// </summary>
    public interface IUrdtServicesReset
    {
        void ResetServices();
    }
}
