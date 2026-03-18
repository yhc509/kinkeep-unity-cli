using System.Diagnostics;
using System.Xml.Linq;
using UnityCli.Protocol;

namespace UnityCli.Cli.Services;

public sealed class BatchModeRunner
{
    public async Task<ResponseEnvelope> RunCompileAsync(string projectRoot, string unityPath, int timeoutMs, string? fallbackReason)
    {
        return await RunWithProjectLockAsync(
            "compile",
            projectRoot,
            timeoutMs,
            fallbackReason,
            async remainingTimeoutMs =>
            {
                var result = await RunProcessAsync(
                    unityPath,
                    [
                        "-batchmode",
                        "-projectPath", projectRoot,
                        "-logFile", "-",
                        "-quit",
                    ],
                    remainingTimeoutMs);

                return BuildBatchResponse("compile", projectRoot, result, fallbackReason, null);
            });
    }

    public async Task<ResponseEnvelope> RunRefreshAsync(string projectRoot, string unityPath, int timeoutMs, string? fallbackReason)
    {
        return await RunRequestAsync(
            projectRoot,
            unityPath,
            new CommandEnvelope
            {
                requestId = Guid.NewGuid().ToString("N"),
                command = ProtocolConstants.CommandRefresh,
                argumentsJson = "{}",
            },
            timeoutMs,
            fallbackReason);
    }

    public async Task<ResponseEnvelope> RunTestsAsync(string projectRoot, string unityPath, string mode, int timeoutMs, string? fallbackReason)
    {
        var resultsFile = Path.Combine(Path.GetTempPath(), $"unity-cli-tests-{Guid.NewGuid():N}.xml");
        try
        {
            return await RunWithProjectLockAsync(
                "run-tests",
                projectRoot,
                timeoutMs,
                fallbackReason,
                async remainingTimeoutMs =>
                {
                    var platform = mode == "play" ? "playmode" : "editmode";
                    var result = await RunProcessAsync(
                        unityPath,
                        [
                            "-batchmode",
                            "-projectPath", projectRoot,
                            "-runTests",
                            "-testPlatform", platform,
                            "-testResults", resultsFile,
                            "-logFile", "-",
                            "-quit",
                        ],
                        remainingTimeoutMs);

                    var summary = File.Exists(resultsFile) ? SummarizeTestResults(resultsFile) : null;
                    return BuildBatchResponse("run-tests", projectRoot, result, fallbackReason, null, summary);
                });
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    public async Task<ResponseEnvelope> RunRequestAsync(
        string projectRoot,
        string unityPath,
        CommandEnvelope request,
        int timeoutMs,
        string? fallbackReason)
    {
        var requestFile = Path.Combine(Path.GetTempPath(), $"unity-cli-request-{Guid.NewGuid():N}.json");
        var resultFile = Path.Combine(Path.GetTempPath(), $"unity-cli-result-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(requestFile, ProtocolJson.Serialize(request));

            return await RunWithProjectLockAsync(
                request.command,
                projectRoot,
                timeoutMs,
                fallbackReason,
                async remainingTimeoutMs =>
                {
                    var result = await RunProcessAsync(
                        unityPath,
                        [
                            "-batchmode",
                            "-projectPath", projectRoot,
                            "-executeMethod", "PUC.Editor.Batch.BatchCommandRunner.RunRequest",
                            "-unityCliRequestFile", requestFile,
                            "-unityCliResultFile", resultFile,
                            "-logFile", "-",
                            "-quit",
                        ],
                        remainingTimeoutMs);

                    return BuildBatchResponse(request.command, projectRoot, result, fallbackReason, resultFile);
                });
        }
        finally
        {
            if (File.Exists(requestFile))
            {
                File.Delete(requestFile);
            }

            if (File.Exists(resultFile))
            {
                File.Delete(resultFile);
            }
        }
    }
    private static object? SummarizeTestResults(string resultsFile)
    {
        var doc = XDocument.Load(resultsFile);
        var root = doc.Root;
        if (root is null)
        {
            return null;
        }

        static int ReadInt(XElement element, string attribute)
        {
            return int.TryParse(element.Attribute(attribute)?.Value, out var value) ? value : 0;
        }

        return new
        {
            total = ReadInt(root, "total"),
            passed = ReadInt(root, "passed"),
            failed = ReadInt(root, "failed"),
            skipped = ReadInt(root, "skipped"),
            inconclusive = ReadInt(root, "inconclusive"),
            duration = root.Attribute("duration")?.Value,
        };
    }

    private static ResponseEnvelope BuildBatchResponse(
        string command,
        string projectRoot,
        ProcessResult result,
        string? fallbackReason,
        string? resultFilePath,
        object? extraData = null)
    {
        if (!string.IsNullOrWhiteSpace(resultFilePath) && File.Exists(resultFilePath))
        {
            var fileContent = File.ReadAllText(resultFilePath);
            var response = ProtocolJson.Deserialize<ResponseEnvelope>(fileContent);
            if (response is not null)
            {
                response.transport = "batch";
                return response;
            }
        }

        var data = new
        {
            command,
            fallbackReason,
            projectRoot,
            exitCode = result.ExitCode,
            stdoutTail = Tail(result.StdOut, 80),
            stderrTail = Tail(result.StdErr, 80),
            extraData,
        };

        return result.ExitCode == 0
            ? ResponseEnvelope.Success(
                Guid.NewGuid().ToString("N"),
                ProtocolConstants.ComputeProjectHash(projectRoot),
                ProtocolJson.Serialize(data),
                result.DurationMs,
                transport: "batch")
            : ResponseEnvelope.Failure(
                Guid.NewGuid().ToString("N"),
                ProtocolConstants.ComputeProjectHash(projectRoot),
                "BATCH_FAILED",
                $"{command} batch 실행이 실패했습니다.",
                retryable: false,
                durationMs: result.DurationMs,
                transport: "batch",
                details: ProtocolJson.Serialize(data));
    }

    private static string[] Tail(string content, int maxLines)
    {
        return content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .TakeLast(maxLines)
            .ToArray();
    }

    private static async Task<ResponseEnvelope> RunWithProjectLockAsync(
        string command,
        string projectRoot,
        int timeoutMs,
        string? fallbackReason,
        Func<int, Task<ResponseEnvelope>> runAsync)
    {
        var overall = Stopwatch.StartNew();

        try
        {
            using var projectLock = await BatchProjectLock.AcquireAsync(projectRoot, timeoutMs);
            var remainingTimeoutMs = timeoutMs - (int)overall.ElapsedMilliseconds;
            if (remainingTimeoutMs <= 0)
            {
                throw new TimeoutException($"batch 실행 준비 시간이 {timeoutMs}ms를 넘겼습니다.");
            }

            return await runAsync(remainingTimeoutMs);
        }
        catch (TimeoutException ex)
        {
            var details = new
            {
                command,
                fallbackReason,
                projectRoot,
                timeoutMs,
                elapsedMs = overall.ElapsedMilliseconds,
                reason = ex.Message,
            };

            return ResponseEnvelope.Failure(
                Guid.NewGuid().ToString("N"),
                ProtocolConstants.ComputeProjectHash(projectRoot),
                "BATCH_TIMEOUT",
                $"{command} batch 실행이 시간 안에 끝나지 않았습니다.",
                retryable: true,
                durationMs: overall.ElapsedMilliseconds,
                transport: ProtocolConstants.TransportBatch,
                details: ProtocolJson.Serialize(details));
        }
    }

    private static async Task<ProcessResult> RunProcessAsync(string fileName, IReadOnlyList<string> arguments, int timeoutMs)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var startedAt = Stopwatch.StartNew();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = new CancellationTokenSource(timeoutMs);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            throw new TimeoutException($"Unity batch 작업이 {timeoutMs}ms 안에 끝나지 않았습니다.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        startedAt.Stop();

        return new ProcessResult(process.ExitCode, stdout, stderr, startedAt.ElapsedMilliseconds);
    }

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr, long DurationMs);
}
