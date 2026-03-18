namespace UnityCli.Cli.Services;

public static class UnityEditorLocator
{
    public static string? TryResolve(string projectRoot)
    {
        var fromEnv = Environment.GetEnvironmentVariable("UNITY_CLI_UNITY_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
        {
            return fromEnv;
        }

        var version = TryReadEditorVersion(projectRoot);
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        if (OperatingSystem.IsMacOS())
        {
            var macPath = $"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity";
            return File.Exists(macPath) ? macPath : null;
        }

        if (OperatingSystem.IsWindows())
        {
            var winPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Unity",
                "Hub",
                "Editor",
                version,
                "Editor",
                "Unity.exe");
            return File.Exists(winPath) ? winPath : null;
        }

        var linuxPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Unity",
            "Hub",
            "Editor",
            version,
            "Editor",
            "Unity");
        return File.Exists(linuxPath) ? linuxPath : null;
    }

    private static string? TryReadEditorVersion(string projectRoot)
    {
        var versionFile = Path.Combine(projectRoot, "ProjectSettings", "ProjectVersion.txt");
        if (!File.Exists(versionFile))
        {
            return null;
        }

        foreach (var line in File.ReadLines(versionFile))
        {
            if (line.StartsWith("m_EditorVersion:", StringComparison.Ordinal))
            {
                return line.Split(':', 2)[1].Trim();
            }
        }

        return null;
    }
}
