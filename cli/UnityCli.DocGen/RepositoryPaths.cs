namespace UnityCli.DocGen;

public static class RepositoryPaths
{
    public static string FindRepoRoot(string startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "KinKeepUnityCli.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from: " + startDirectory);
    }
}
