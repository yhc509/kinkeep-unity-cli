using System.IO;
using System.Text.Json;
using UnityCli.Protocol;

namespace UnityCli.Cli.Tests;

public sealed class CliAppTests
{
    private static readonly SemaphoreSlim ConsoleLock = new(1, 1);

    [Fact]
    public async Task RunAsync_JsonUnknownCommand_WritesStructuredErrorToStdoutOnly()
    {
        var result = await InvokeAsync(["--json", "stats"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);

        var response = ParseResponse(result.Stdout);
        Assert.Equal("error", response.status);
        Assert.Equal("CLI_USAGE", response.error?.code);
        Assert.Contains("알 수 없는 명령", response.error?.message);

        var details = ParseDetails(response.error?.details);
        Assert.Equal("usage: unity-cli [--json] [--project <path>] <command> [options]", details.GetProperty("usage").GetString());
        Assert.Equal("status", details.GetProperty("suggestions")[0].GetString());
    }

    [Fact]
    public async Task RunAsync_JsonUnknownSubcommand_IncludesSuggestionsAndAvailableCommands()
    {
        var result = await InvokeAsync(["--json", "asset", "delte"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);

        var response = ParseResponse(result.Stdout);
        Assert.Equal("CLI_USAGE", response.error?.code);
        Assert.Contains("asset 하위 명령", response.error?.message);

        var details = ParseDetails(response.error?.details);
        Assert.Equal("asset delete", details.GetProperty("suggestions")[0].GetString());
        Assert.Equal("asset create", details.GetProperty("availableCommands")[0].GetString());
    }

    [Fact]
    public async Task RunAsync_JsonMissingRequiredOption_IncludesCommandUsage()
    {
        var result = await InvokeAsync(["--json", "asset", "delete", "--path", "Assets/Test.asset"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);

        var response = ParseResponse(result.Stdout);
        Assert.Equal("CLI_USAGE", response.error?.code);
        Assert.Contains("--force", response.error?.message);

        var details = ParseDetails(response.error?.details);
        Assert.Equal(
            "usage: unity-cli [--json] [--project <path>] asset delete --path <Assets/...> --force",
            details.GetProperty("usage").GetString());
    }

    [Fact]
    public async Task RunAsync_JsonInvalidOption_IncludesCommandUsage()
    {
        var result = await InvokeAsync(["status", "--json", "--verbose"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);

        var response = ParseResponse(result.Stdout);
        Assert.Equal("CLI_USAGE", response.error?.code);
        Assert.Contains("지원하지 않는 옵션", response.error?.message);

        var details = ParseDetails(response.error?.details);
        Assert.Equal(
            "usage: unity-cli [--json] [--project <path>] status",
            details.GetProperty("usage").GetString());
    }

    [Fact]
    public async Task RunAsync_JsonNoTarget_KeepsFailureStructuredOnStdout()
    {
        var result = await InvokeAsync(["--json", "compile"]);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);

        var response = ParseResponse(result.Stdout);
        Assert.Equal("error", response.status);
        Assert.Equal("NO_TARGET", response.error?.code);
        Assert.Equal("cli", response.transport);
    }

    [Fact]
    public async Task RunAsync_JsonProjectOverride_DoesNotFallBackToActiveProject()
    {
        using var temp = new TempDirectory();
        string projectRoot = CreateUnityProject(temp.Path, "SampleProject");
        string otherProjectRoot = CreateUnityProject(temp.Path, "OtherProject");
        string otherProjectHash = ProtocolConstants.ComputeProjectHash(otherProjectRoot);
        string expectedProjectHash = ProtocolConstants.ComputeProjectHash(projectRoot);
        string registryContents =
            $$"""
            {"activeProjectHash":"{{otherProjectHash}}","instances":[{"projectRoot":"{{otherProjectRoot.Replace("\\", "\\\\")}}","projectName":"OtherProject","projectHash":"{{otherProjectHash}}","pipeName":"{{ProtocolConstants.BuildPipeName(otherProjectHash).Replace("\\", "\\\\")}}","editorProcessId":1234,"unityVersion":"6000.3.10f1","state":"idle","lastSeenUtc":"2026-04-02T03:19:16.4545650+00:00","capabilities":[]}]}
            """;

        var result = await InvokeAsync(["--json", "--project", projectRoot, "compile"], registryContents: registryContents);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);

        var response = ParseResponse(result.Stdout);
        Assert.Equal("LIVE_UNAVAILABLE", response.error?.code);
        Assert.Equal(expectedProjectHash, response.target);
        Assert.DoesNotContain(otherProjectHash, result.Stdout);
    }

    [Fact]
    public async Task RunAsync_JsonUnexpectedException_WritesCliErrorToStdoutOnly()
    {
        var result = await InvokeAsync(
            ["--json", "compile"],
            registryContents: "{\"instances\":");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);

        var response = ParseResponse(result.Stdout);
        Assert.Equal("CLI_ERROR", response.error?.code);
        Assert.Contains("JsonException", response.error?.details);
    }

    [Fact]
    public async Task RunAsync_JsonRawPayloadValidation_IsNotMaskedByNoTarget()
    {
        var result = await InvokeAsync(["--json", "raw", "--json", "{"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);

        var response = ParseResponse(result.Stdout);
        Assert.Equal("CLI_USAGE", response.error?.code);
        Assert.Contains("올바른 JSON", response.error?.message);

        var details = ParseDetails(response.error?.details);
        Assert.Equal(
            "usage: unity-cli [--json] [--project <path>] raw --json '{\"command\":\"status\",\"arguments\":{}}'",
            details.GetProperty("usage").GetString());
    }

    [Fact]
    public async Task RunAsync_UnknownCommand_ShowsSuggestionInStderr()
    {
        var result = await InvokeAsync(["stats"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Contains("알 수 없는 명령입니다: stats", result.Stderr);
        Assert.Contains("유사한 명령:", result.Stderr);
        Assert.Contains("status", result.Stderr);
    }

    [Fact]
    public async Task RunAsync_UnknownSubcommand_ShowsAvailableChoicesInStderr()
    {
        var result = await InvokeAsync(["asset", "delte"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Contains("알 수 없는 asset 하위 명령입니다: delte", result.Stderr);
        Assert.Contains("유사한 `asset` 하위 명령:", result.Stderr);
        Assert.Contains("asset delete", result.Stderr);
        Assert.Contains("사용 가능한 `asset` 하위 명령:", result.Stderr);
        Assert.Contains("asset create", result.Stderr);
    }

    [Fact]
    public async Task RunAsync_MissingRequiredOption_ShowsCommandUsageInStderr()
    {
        var result = await InvokeAsync(["asset", "delete", "--path", "Assets/Test.asset"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Contains("--force", result.Stderr);
        Assert.Contains(
            "usage: unity-cli [--json] [--project <path>] asset delete --path <Assets/...> --force",
            result.Stderr);
    }

    [Fact]
    public async Task RunAsync_InvalidOption_ShowsCommandUsageInStderr()
    {
        var result = await InvokeAsync(["status", "--verbose"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Contains("지원하지 않는 옵션입니다: --verbose", result.Stderr);
        Assert.Contains("usage: unity-cli [--json] [--project <path>] status", result.Stderr);
    }

    private static ResponseEnvelope ParseResponse(string stdout)
    {
        Assert.False(string.IsNullOrWhiteSpace(stdout));
        return ProtocolJson.Deserialize<ResponseEnvelope>(stdout.Trim());
    }

    private static JsonElement ParseDetails(string? details)
    {
        Assert.False(string.IsNullOrWhiteSpace(details));
        return JsonSerializer.Deserialize<JsonElement>(details!);
    }

    private static async Task<CliInvocationResult> InvokeAsync(string[] args, string? registryContents = null)
    {
        await ConsoleLock.WaitAsync();

        try
        {
            using var temp = new TempDirectory();
            string registryPath = Path.Combine(temp.Path, "instances.json");
            if (registryContents is not null)
            {
                File.WriteAllText(registryPath, registryContents);
            }

            string? originalRegistryPath = Environment.GetEnvironmentVariable("UNITY_CLI_REGISTRY_PATH");
            string originalCurrentDirectory = Environment.CurrentDirectory;
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            try
            {
                Environment.SetEnvironmentVariable("UNITY_CLI_REGISTRY_PATH", registryPath);
                Environment.CurrentDirectory = temp.Path;
                Console.SetOut(stdout);
                Console.SetError(stderr);

                int exitCode = await UnityCli.Cli.CliApp.RunAsync(args);
                return new CliInvocationResult(exitCode, stdout.ToString(), stderr.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
                Environment.CurrentDirectory = originalCurrentDirectory;
                Environment.SetEnvironmentVariable("UNITY_CLI_REGISTRY_PATH", originalRegistryPath);
            }
        }
        finally
        {
            ConsoleLock.Release();
        }
    }

    private static string CreateUnityProject(string root, string name)
    {
        string projectRoot = Path.Combine(root, name);
        Directory.CreateDirectory(Path.Combine(projectRoot, "Assets"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "Packages"));
        return projectRoot;
    }

    private sealed record CliInvocationResult(int ExitCode, string Stdout, string Stderr);
}
