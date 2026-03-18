using System.Diagnostics;
using UnityCli.Protocol;

namespace UnityCli.Cli.Services;

public sealed class BatchProjectLock : IDisposable
{
    private readonly FileStream _stream;
    private readonly string _lockFilePath;
    private bool _disposed;

    private BatchProjectLock(FileStream stream, string lockFilePath)
    {
        _stream = stream;
        _lockFilePath = lockFilePath;
    }

    public static async Task<BatchProjectLock> AcquireAsync(string projectRoot, int timeoutMs, CancellationToken cancellationToken = default)
    {
        var normalizedTimeoutMs = Math.Max(1, timeoutMs);
        var deadline = Stopwatch.StartNew();
        var lockFilePath = Path.Combine(
            Path.GetTempPath(),
            $"unity-cli-batch-{ProtocolConstants.ComputeProjectHash(projectRoot)}.lock");

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var stream = new FileStream(
                    lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
                return new BatchProjectLock(stream, lockFilePath);
            }
            catch (IOException)
            {
                if (deadline.ElapsedMilliseconds >= normalizedTimeoutMs)
                {
                    throw new TimeoutException($"같은 프로젝트의 다른 batch 작업이 {normalizedTimeoutMs}ms 안에 끝나지 않았습니다.");
                }

                await Task.Delay(100, cancellationToken);
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                if (deadline.ElapsedMilliseconds >= normalizedTimeoutMs)
                {
                    throw new TimeoutException($"같은 프로젝트의 다른 batch 작업이 {normalizedTimeoutMs}ms 안에 끝나지 않았습니다.");
                }

                await Task.Delay(100, cancellationToken);
                continue;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stream.Dispose();

        try
        {
            File.Delete(_lockFilePath);
        }
        catch
        {
        }
    }
}
