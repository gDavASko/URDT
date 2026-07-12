namespace KBP.URDT.Transport
{
    /// <summary>
    /// Handles one <c>action</c>. Runs strictly on the Unity main thread
    /// (invariant I6); the <see cref="CommandRouter"/> isolates any thrown exception
    /// as <see cref="ErrorCodes.E_INTERNAL"/>.
    /// </summary>
    public interface ICommandHandler
    {
        Response Handle(Command command);
    }
}
