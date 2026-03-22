using ProtocolCliCommandCatalog = UnityCli.Protocol.CliCommandCatalog;

namespace UnityCli.Cli.Services;

public static class CliCommandMetadata
{
    public static string BuildHelpText()
    {
        return ProtocolCliCommandCatalog.BuildHelpText().TrimEnd();
    }

    public static bool DetectJsonOutput(string[] args)
    {
        var tokens = new Queue<string>(args);
        while (tokens.Count > 0)
        {
            if (tokens.Peek() == "--json")
            {
                return true;
            }

            if (tokens.Peek() == "--project")
            {
                tokens.Dequeue();
                if (tokens.Count > 0)
                {
                    tokens.Dequeue();
                }

                continue;
            }

            break;
        }

        if (tokens.Count == 0)
        {
            return false;
        }

        var command = tokens.Dequeue().ToLowerInvariant();
        if (command is "raw" or "custom")
        {
            return false;
        }

        return tokens.Any(token => token == "--json");
    }
}
