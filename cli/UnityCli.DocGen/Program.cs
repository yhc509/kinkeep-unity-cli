namespace UnityCli.DocGen;

public static class Program
{
    public static int Main(string[] args)
    {
        string repoRoot = RepositoryPaths.FindRepoRoot(Environment.CurrentDirectory);
        string outputPath = Path.Combine(repoRoot, CliReferenceDocumentRenderer.OutputRelativePath);
        string generated = CliReferenceDocumentRenderer.Render();

        if (args.Contains("--write", StringComparer.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, generated);
            Console.WriteLine("Wrote " + outputPath);
            return 0;
        }

        if (args.Contains("--check", StringComparer.Ordinal))
        {
            string current = File.Exists(outputPath) ? File.ReadAllText(outputPath) : string.Empty;
            if (!string.Equals(Normalize(current), Normalize(generated), StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Generated CLI reference is stale: " + outputPath);
                Console.Error.WriteLine("Run `dotnet run --project cli/UnityCli.DocGen -- --write`.");
                return 1;
            }

            Console.WriteLine("Up to date: " + outputPath);
            return 0;
        }

        Console.Write(generated);
        return 0;
    }

    private static string Normalize(string value)
    {
        return value.Replace("\r\n", "\n");
    }
}
