namespace KBP.URDT
{
    /// <summary>
    /// Observable lifecycle state of the URDT host.
    /// </summary>
    public enum UrdtServerState
    {
        Stopped = 0,
        Starting = 1,
        Listening = 2,
        Ready = 3,
        Degraded = 4,
        Stopping = 5,
        Faulted = 6
    }
}
