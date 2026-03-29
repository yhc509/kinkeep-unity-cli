#nullable enable
using System;

namespace UnityCli.Protocol
{
    [Serializable]
    public sealed class CommandEnvelope
    {
        public string requestId = string.Empty;
        public string command = string.Empty;
        public string argumentsJson = "{}";
    }

    [Serializable]
    public sealed class ResponseEnvelope
    {
        public string requestId = string.Empty;
        public string? target;
        public string status = ProtocolConstants.StatusSuccess;
        public long durationMs;
        public string? dataJson;
        public ProtocolError? error;
        public bool retryable;
        public string transport = ProtocolConstants.TransportLive;

        public static ResponseEnvelope Success(string requestId, string? target, string? dataJson, long durationMs, string transport = ProtocolConstants.TransportLive)
        {
            return new ResponseEnvelope
            {
                requestId = requestId,
                target = target,
                status = ProtocolConstants.StatusSuccess,
                durationMs = durationMs,
                dataJson = dataJson,
                transport = transport,
            };
        }

        public static ResponseEnvelope Failure(
            string requestId,
            string? target,
            string code,
            string message,
            bool retryable,
            long durationMs = 0,
            string transport = ProtocolConstants.TransportLive,
            string? details = null)
        {
            return new ResponseEnvelope
            {
                requestId = requestId,
                target = target,
                status = ProtocolConstants.StatusError,
                durationMs = durationMs,
                retryable = retryable,
                transport = transport,
                error = new ProtocolError
                {
                    code = code,
                    message = message,
                    details = details,
                },
            };
        }
    }

    [Serializable]
    public sealed class ProtocolError
    {
        public string code = string.Empty;
        public string message = string.Empty;
        public string? details;
    }
}
