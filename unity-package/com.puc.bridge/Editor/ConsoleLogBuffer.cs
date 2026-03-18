using System;
using System.Collections.Generic;
using System.Linq;
using UnityCli.Protocol;
using UnityEngine;

namespace PUC.Editor
{
    internal static class ConsoleLogBuffer
    {
        private static readonly object Sync = new();
        private static readonly Queue<ConsoleLogEntry> Entries = new();
        private const int MaxEntries = 512;

        public static void Start()
        {
            Application.logMessageReceivedThreaded -= OnLogReceived;
            Application.logMessageReceivedThreaded += OnLogReceived;
        }

        public static void Stop()
        {
            Application.logMessageReceivedThreaded -= OnLogReceived;
            lock (Sync)
            {
                Entries.Clear();
            }
        }

        public static ConsoleLogEntry[] Read(int limit, string type)
        {
            lock (Sync)
            {
                IEnumerable<ConsoleLogEntry> query = Entries;
                if (!string.IsNullOrWhiteSpace(type))
                {
                    query = query.Where(entry => string.Equals(entry.type, type, StringComparison.OrdinalIgnoreCase));
                }

                return query.TakeLast(Math.Max(1, limit)).ToArray();
            }
        }

        private static void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            lock (Sync)
            {
                Entries.Enqueue(new ConsoleLogEntry
                {
                    timestampUtc = DateTimeOffset.UtcNow.ToString("O"),
                    type = type.ToString(),
                    message = condition,
                    stackTrace = stackTrace,
                });

                while (Entries.Count > MaxEntries)
                {
                    Entries.Dequeue();
                }
            }
        }
    }

}
