namespace KBP.URDT.Transport
{
    /// <summary>
    /// Stable, machine-readable error codes (TZ §7.5, protocol-spec §6). Each error
    /// carries a code + human message + the echoed request id.
    /// </summary>
    public static class ErrorCodes
    {
        public const string E_INTERNAL = "E_INTERNAL";
        public const string E_UNKNOWN_ACTION = "E_UNKNOWN_ACTION";
        public const string E_BAD_PAYLOAD = "E_BAD_PAYLOAD";
        public const string E_SHUTTING_DOWN = "E_SHUTTING_DOWN";
        public const string E_UNSUPPORTED = "E_UNSUPPORTED";
        public const string E_TIME_MODE = "E_TIME_MODE";
        public const string E_API_VERSION = "E_API_VERSION";
        public const string E_UNAUTHORIZED = "E_UNAUTHORIZED";
        public const string E_TIMEOUT = "E_TIMEOUT";
        public const string E_NOT_FOUND = "E_NOT_FOUND";
        public const string E_BAD_REQUEST = "E_BAD_REQUEST";
        public const string E_UNKNOWN_EVENT = "E_UNKNOWN_EVENT";
        public const string E_NOT_HITTABLE = "E_NOT_HITTABLE";
        public const string E_OFFSCREEN = "E_OFFSCREEN";
        public const string E_NOT_INTERACTABLE = "E_NOT_INTERACTABLE";
        public const string E_WRONG_INSTANCE = "E_WRONG_INSTANCE";
        public const string E_RUNTIME_UNAVAILABLE = "E_RUNTIME_UNAVAILABLE";
    }
}
