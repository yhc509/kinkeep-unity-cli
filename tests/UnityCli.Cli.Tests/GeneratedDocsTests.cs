using UnityCli.DocGen;

namespace UnityCli.Cli.Tests;

public sealed class GeneratedDocsTests
{
    [Fact]
    public void CliReference_IsUpToDate()
    {
        string repoRoot = RepositoryPaths.FindRepoRoot(AppContext.BaseDirectory);
        string outputPath = Path.Combine(repoRoot, CliReferenceDocumentRenderer.OutputRelativePath);

        Assert.True(
            File.Exists(outputPath),
            "Generated CLI reference is missing. Run `dotnet run --project cli/UnityCli.DocGen -- --write`.");

        string expected = Normalize(CliReferenceDocumentRenderer.Render());
        string actual = Normalize(File.ReadAllText(outputPath));

        Assert.True(
            string.Equals(expected, actual, StringComparison.Ordinal),
            "Generated CLI reference is stale. Run `dotnet run --project cli/UnityCli.DocGen -- --write`.");
    }

    private static string Normalize(string value)
    {
        return value.Replace("\r\n", "\n");
    }
}
