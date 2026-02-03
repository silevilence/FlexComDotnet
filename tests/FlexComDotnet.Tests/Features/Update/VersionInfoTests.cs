using FlexComDotnet.Core.Features.Update.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Update;

/// <summary>
/// VersionInfo 模型测试
/// </summary>
public class VersionInfoTests
{
    #region Parse Tests

    [Theory]
    [InlineData("1.0.0", 1, 0, 0, null)]
    [InlineData("v1.0.0", 1, 0, 0, null)]
    [InlineData("V1.0.0", 1, 0, 0, null)]
    [InlineData("2.3.4", 2, 3, 4, null)]
    [InlineData("v10.20.30", 10, 20, 30, null)]
    [InlineData("1.0.0-alpha", 1, 0, 0, "alpha")]
    [InlineData("v1.0.0-beta.1", 1, 0, 0, "beta.1")]
    [InlineData("2.0.0-rc.2", 2, 0, 0, "rc.2")]
    public void Parse_ValidVersionStrings_ShouldParseCorrectly(
        string input, int expectedMajor, int expectedMinor, int expectedPatch, string? expectedPrerelease)
    {
        // Act
        var version = VersionInfo.Parse(input);

        // Assert
        version.Major.Should().Be(expectedMajor);
        version.Minor.Should().Be(expectedMinor);
        version.Patch.Should().Be(expectedPatch);
        version.Prerelease.Should().Be(expectedPrerelease);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_EmptyOrNull_ShouldReturnEmptyVersion(string? input)
    {
        // Act
        var version = VersionInfo.Parse(input!);

        // Assert
        version.Major.Should().Be(0);
        version.Minor.Should().Be(0);
        version.Patch.Should().Be(0);
    }

    [Fact]
    public void Parse_VersionWithLeadingV_ShouldStripPrefix()
    {
        // Act
        var version = VersionInfo.Parse("v1.2.3");

        // Assert
        version.ToString().Should().Be("1.2.3");
    }

    [Fact]
    public void Parse_PartialVersion_ShouldHandleMissingParts()
    {
        // Act
        var version1 = VersionInfo.Parse("1");
        var version2 = VersionInfo.Parse("1.2");

        // Assert
        version1.Major.Should().Be(1);
        version1.Minor.Should().Be(0);
        version1.Patch.Should().Be(0);

        version2.Major.Should().Be(1);
        version2.Minor.Should().Be(2);
        version2.Patch.Should().Be(0);
    }

    #endregion

    #region TryParse Tests

    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("v2.3.4", true)]
    [InlineData("0.0.0", false)]
    [InlineData("", false)]
    public void TryParse_ShouldReturnCorrectResult(string input, bool expectedResult)
    {
        // Act
        var result = VersionInfo.TryParse(input, out var version);

        // Assert
        result.Should().Be(expectedResult);
    }

    #endregion

    #region CompareTo Tests

    [Theory]
    [InlineData("2.0.0", "1.0.0", 1)]   // Major version higher
    [InlineData("1.0.0", "2.0.0", -1)]  // Major version lower
    [InlineData("1.1.0", "1.0.0", 1)]   // Minor version higher
    [InlineData("1.0.0", "1.1.0", -1)]  // Minor version lower
    [InlineData("1.0.1", "1.0.0", 1)]   // Patch version higher
    [InlineData("1.0.0", "1.0.1", -1)]  // Patch version lower
    [InlineData("1.0.0", "1.0.0", 0)]   // Equal versions
    public void CompareTo_VersionNumbers_ShouldCompareCorrectly(
        string version1, string version2, int expectedSign)
    {
        // Arrange
        var v1 = VersionInfo.Parse(version1);
        var v2 = VersionInfo.Parse(version2);

        // Act
        var result = v1.CompareTo(v2);

        // Assert
        Math.Sign(result).Should().Be(expectedSign);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0-alpha", 1)]      // Release > prerelease
    [InlineData("1.0.0-alpha", "1.0.0", -1)]     // Prerelease < release
    [InlineData("1.0.0-beta", "1.0.0-alpha", 1)] // beta > alpha (alphabetical)
    [InlineData("1.0.0-rc", "1.0.0-beta", 1)]    // rc > beta
    public void CompareTo_PrereleaseVersions_ShouldCompareCorrectly(
        string version1, string version2, int expectedSign)
    {
        // Arrange
        var v1 = VersionInfo.Parse(version1);
        var v2 = VersionInfo.Parse(version2);

        // Act
        var result = v1.CompareTo(v2);

        // Assert
        Math.Sign(result).Should().Be(expectedSign);
    }

    #endregion

    #region IsNewerThan Tests

    [Theory]
    [InlineData("2.0.0", "1.0.0", true)]
    [InlineData("1.0.0", "2.0.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0-beta", true)]
    public void IsNewerThan_ShouldReturnCorrectResult(string version1, string version2, bool expected)
    {
        // Arrange
        var v1 = VersionInfo.Parse(version1);
        var v2 = VersionInfo.Parse(version2);

        // Act
        var result = v1.IsNewerThan(v2);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region ToString Tests

    [Theory]
    [InlineData(1, 0, 0, null, "1.0.0")]
    [InlineData(2, 3, 4, null, "2.3.4")]
    [InlineData(1, 0, 0, "alpha", "1.0.0-alpha")]
    [InlineData(1, 0, 0, "beta.1", "1.0.0-beta.1")]
    public void ToString_ShouldFormatCorrectly(
        int major, int minor, int patch, string? prerelease, string expected)
    {
        // Arrange
        var version = new VersionInfo
        {
            Major = major,
            Minor = minor,
            Patch = patch,
            Prerelease = prerelease,
            RawVersion = expected
        };

        // Act
        var result = version.ToString();

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Empty Tests

    [Fact]
    public void Empty_ShouldReturnZeroVersion()
    {
        // Act
        var empty = VersionInfo.Empty;

        // Assert
        empty.Major.Should().Be(0);
        empty.Minor.Should().Be(0);
        empty.Patch.Should().Be(0);
        empty.Prerelease.Should().BeNull();
    }

    #endregion
}
