using UnityCli.Protocol;

namespace UnityCli.Cli.Services;

public sealed class UnityProjectLocator
{
    public string? TryFindProjectRoot(string startDirectory)
    {
        var current = new DirectoryInfo(ProtocolConstants.GetCanonicalPath(startDirectory));
        while (current is not null)
        {
            if (LooksLikeUnityProject(current.FullName))
            {
                return ProtocolConstants.GetCanonicalPath(current.FullName);
            }

            current = current.Parent;
        }

        foreach (var child in Directory.EnumerateDirectories(startDirectory))
        {
            if (LooksLikeUnityProject(child))
            {
                return ProtocolConstants.GetCanonicalPath(child);
            }
        }

        return null;
    }

    public bool LooksLikeUnityProject(string path)
    {
        return Directory.Exists(Path.Combine(path, "Assets")) &&
               Directory.Exists(Path.Combine(path, "Packages"));
    }
}
