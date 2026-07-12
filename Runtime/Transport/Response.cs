namespace KBP.URDT.Transport
{
    /// <summary>
    /// Response envelope correlated to a <see cref="Command"/> by <see cref="Id"/>.
    /// Either a success (<see cref="Ok"/> true, <see cref="Data"/> set) or a structured
    /// error (<see cref="ErrorCode"/> + <see cref="ErrorMessage"/> + optional
    /// <see cref="ErrorDetails"/>).
    /// </summary>
    public sealed class Response
    {
        private Response(string id, bool ok, object data, string errorCode, string errorMessage, object errorDetails)
        {
            Id = id;
            Ok = ok;
            Data = data;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            ErrorDetails = errorDetails;
        }

        public string Id { get; }

        public bool Ok { get; }

        public object Data { get; }

        public string ErrorCode { get; }

        public string ErrorMessage { get; }

        public object ErrorDetails { get; }

        public static Response Success(string id, object data = null)
        {
            return new Response(id, true, data, null, null, null);
        }

        public static Response Error(string id, string errorCode, string errorMessage, object errorDetails = null)
        {
            return new Response(id, false, null, errorCode, errorMessage, errorDetails);
        }
    }
}
