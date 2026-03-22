using ProtocolCliCommandCatalog = UnityCli.Protocol.CliCommandCatalog;

namespace UnityCli.Cli.Services;

public static class CliCommandMetadata
{
    public static string BuildHelpText()
    {
        return ProtocolCliCommandCatalog.BuildHelpText().TrimEnd();
    }
}
