using UnityCli.Cli.Models;
using ProtocolCliCommandCatalog = UnityCli.Protocol.CliCommandCatalog;

namespace UnityCli.Cli.Services;

public static class CliCommandMetadata
{
    public static string BuildHelpText()
    {
        return ProtocolCliCommandCatalog.BuildHelpText().TrimEnd();
    }

    public static OutputMode DetectOutputMode(string[] args)
    {
        return CliArgumentParser.DetectOutputMode(args);
    }

    public static bool DetectJsonOutput(string[] args)
    {
        return DetectOutputMode(args) == OutputMode.Json;
    }
}
