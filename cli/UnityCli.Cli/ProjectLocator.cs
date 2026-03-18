using UnityCli.Cli.Services;
using UnityCli.Protocol;

namespace UnityCli.Cli;

public static class ProjectLocator
{
    public static string? FindNearestProjectRoot(string startDirectory)
    {
        return new UnityProjectLocator().TryFindProjectRoot(startDirectory);
    }

    public static string NormalizeProjectRoot(string projectRoot)
    {
        return Path.GetFullPath(projectRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static string ComputeProjectHash(string projectRoot)
    {
        return ProtocolConstants.ComputeProjectHash(projectRoot);
    }
}
