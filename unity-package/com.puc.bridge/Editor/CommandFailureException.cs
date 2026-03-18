using System;

namespace PUC.Editor
{
    internal sealed class CommandFailureException : Exception
    {
        public CommandFailureException(string errorCode, string message, string details = null, bool retryable = false)
            : base(message)
        {
            ErrorCode = errorCode;
            Details = details;
            Retryable = retryable;
        }

        public string ErrorCode { get; private set; }
        public string Details { get; private set; }
        public bool Retryable { get; private set; }
    }
}
