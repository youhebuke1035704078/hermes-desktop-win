using HermesDesktop.Services;
using Xunit;

namespace HermesDesktop.Tests;

public class AppUpdaterServiceTests
{
    [Theory]
    [InlineData("v2026.04.16.1", 2026, 4, 16, 1)]
    [InlineData("1.2.0", 1, 2, 0, 0)]
    [InlineData("bad", 0, 0, 0, 0)]
    public void NormalizeVersion_ParsesReleaseTags(string raw, int major, int minor, int build, int revision)
    {
        Assert.Equal(new Version(major, minor, build, revision), AppUpdaterService.NormalizeVersion(raw));
    }

    [Fact]
    public void IsNewer_ComparesNormalizedVersions()
    {
        Assert.True(AppUpdaterService.IsNewer(new Version(2026, 4, 16, 2), new Version(2026, 4, 16, 1)));
        Assert.False(AppUpdaterService.IsNewer(new Version(2026, 4, 16, 1), new Version(2026, 4, 16, 1)));
    }
}
