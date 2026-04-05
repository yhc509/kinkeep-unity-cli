#nullable enable
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace UnityCli.Protocol
{
    public static class InstanceRegistryFile
    {
        private const int MaxRetryCount = 6;

        public static InstanceRegistry Load(string filePath)
        {
            string fullPath = EnsureDirectory(filePath);
            IOException? lastException = null;

            for (int attempt = 0; attempt < MaxRetryCount; attempt++)
            {
                try
                {
                    if (!File.Exists(fullPath))
                    {
                        return new InstanceRegistry();
                    }

                    using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    string json = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return new InstanceRegistry();
                    }

                    return NormalizeRegistry(ProtocolJson.Deserialize<InstanceRegistry>(json));
                }
                catch (IOException exception)
                {
                    lastException = exception;
                    Thread.Sleep((attempt + 1) * 25);
                }
            }

            if (lastException != null)
            {
                throw lastException;
            }

            return new InstanceRegistry();
        }

        public static void Save(string filePath, InstanceRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException("registry");
            }

            string fullPath = EnsureDirectory(filePath);
            string lockPath = fullPath + ".lock";
            IOException? lastException = null;

            for (int attempt = 0; attempt < MaxRetryCount; attempt++)
            {
                try
                {
                    using (new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    {
                        WriteAtomically(fullPath, NormalizeRegistry(registry));
                        return;
                    }
                }
                catch (IOException exception)
                {
                    lastException = exception;
                    Thread.Sleep((attempt + 1) * 25);
                }
            }

            if (lastException != null)
            {
                throw lastException;
            }
        }

        public static void Update(string filePath, Func<InstanceRegistry, InstanceRegistry> update)
        {
            if (update == null)
            {
                throw new ArgumentNullException("update");
            }

            string fullPath = EnsureDirectory(filePath);
            string lockPath = fullPath + ".lock";
            IOException? lastException = null;

            for (int attempt = 0; attempt < MaxRetryCount; attempt++)
            {
                try
                {
                    using (new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    {
                        InstanceRegistry current = Load(fullPath);
                        InstanceRegistry next = NormalizeRegistry(update(current));
                        WriteAtomically(fullPath, next);
                        return;
                    }
                }
                catch (IOException exception)
                {
                    lastException = exception;
                    Thread.Sleep((attempt + 1) * 25);
                }
            }

            if (lastException != null)
            {
                throw lastException;
            }
        }

        private static void WriteAtomically(string fullPath, InstanceRegistry registry)
        {
            string tempPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(ProtocolJson.Serialize(registry));
                }

                if (File.Exists(fullPath))
                {
                    File.Replace(tempPath, fullPath, null);
                }
                else
                {
                    File.Move(tempPath, fullPath);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private static InstanceRegistry NormalizeRegistry(InstanceRegistry? registry)
        {
            if (registry == null)
            {
                registry = new InstanceRegistry();
            }

            if (registry.instances == null)
            {
                registry.instances = Array.Empty<InstanceRecord>();
            }

            return registry;
        }

        private static string EnsureDirectory(string filePath)
        {
            string fullPath = Path.GetFullPath(filePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return fullPath;
        }
    }
}
