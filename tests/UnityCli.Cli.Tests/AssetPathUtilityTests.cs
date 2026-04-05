using UnityCli.Protocol;

namespace UnityCli.Cli.Tests;

public sealed class AssetPathUtilityTests
{
    [Theory]
    [InlineData(" Assets/Materials/Wood.mat ", "Assets/Materials/Wood.mat")]
    [InlineData("Assets/Materials/Wood.mat/", "Assets/Materials/Wood.mat")]
    [InlineData("Assets\\Materials\\Wood.mat", "Assets/Materials/Wood.mat")]
    public void Normalize_AcceptsAssetsPaths(string input, string expected)
    {
        Assert.Equal(expected, AssetPathUtility.Normalize(input));
    }

    [Theory]
    [InlineData(" Packages/com.test/Runtime/Foo.asset ", "Packages/com.test/Runtime/Foo.asset")]
    [InlineData("Packages/com.test/Runtime/Foo.asset/", "Packages/com.test/Runtime/Foo.asset")]
    [InlineData("Packages\\com.test\\Runtime\\Foo.asset", "Packages/com.test/Runtime/Foo.asset")]
    public void Normalize_WithAllowPackages_AcceptsPackagePaths(string input, string expected)
    {
        Assert.Equal(expected, AssetPathUtility.Normalize(input, allowPackages: true));
    }

    [Fact]
    public void Normalize_WithoutAllowPackages_RejectsPackagePaths()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            AssetPathUtility.Normalize("Packages/com.test/Runtime/Foo.asset"));

        Assert.Equal("asset 경로는 `Assets/...` 형식이어야 합니다.", exception.Message);
    }

    [Fact]
    public void Normalize_WithAllowPackages_RejectsUnsupportedRoots()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            AssetPathUtility.Normalize("ProjectSettings/TagManager.asset", allowPackages: true));

        Assert.Equal("asset 경로는 `Assets/...` 또는 `Packages/...` 형식이어야 합니다.", exception.Message);
    }
}
