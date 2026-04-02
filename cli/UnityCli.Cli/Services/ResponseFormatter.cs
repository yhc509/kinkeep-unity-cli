using System.Text.Json;
using System.Text.Encodings.Web;
using UnityCli.Cli.Models;
using UnityCli.Protocol;

namespace UnityCli.Cli.Services;

public static class ResponseFormatter
{
    private static readonly JsonSerializerOptions PrettyPrintOptions = new(ProtocolJson.Default)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Format(ParsedCommand parsed, ResponseEnvelope response)
    {
        if (parsed.JsonOutput)
        {
            return ProtocolJson.Serialize(response);
        }

        if (response.status != "success")
        {
            return BuildErrorText(response);
        }

        var lines = new List<string>
        {
            $"status: {response.status}",
            $"transport: {response.transport}",
        };

        if (!string.IsNullOrWhiteSpace(response.target))
        {
            lines.Add($"target: {response.target}");
        }

        if (response.durationMs > 0)
        {
            lines.Add($"durationMs: {response.durationMs}");
        }

        if (!string.IsNullOrWhiteSpace(response.dataJson))
        {
            lines.Add("data:");
            lines.Add(PrettyJson(response.dataJson));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildErrorText(ResponseEnvelope response)
    {
        var lines = new List<string>
        {
            $"status: {response.status}",
            $"transport: {response.transport}",
        };

        if (!string.IsNullOrWhiteSpace(response.target))
        {
            lines.Add($"target: {response.target}");
        }

        if (response.error is not null)
        {
            lines.Add($"errorCode: {response.error.code}");
            lines.Add($"message: {response.error.message}");
            if (!string.IsNullOrWhiteSpace(response.error.details))
            {
                lines.Add("details:");
                lines.Add(TryPrettyJson(response.error.details));
            }
        }

        lines.Add($"retryable: {response.retryable.ToString().ToLowerInvariant()}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string PrettyJson(string json)
    {
        var element = JsonSerializer.Deserialize<JsonElement>(json, ProtocolJson.Default);
        return JsonSerializer.Serialize(element, PrettyPrintOptions);
    }

    private static string TryPrettyJson(string input)
    {
        try
        {
            return PrettyJson(input);
        }
        catch
        {
            return input;
        }
    }
}
