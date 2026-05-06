using HermesDesktop.Services;
using Xunit;

namespace HermesDesktop.Tests;

public class SshConfigParserTests
{
    [Fact]
    public void ParseLines_ImportsConcreteHostsAndSkipsWildcards()
    {
        var entries = SshConfigParser.ParseLines([
            "Host *",
            "  User ignored",
            "Host prod",
            "  HostName 10.0.0.5",
            "  User deploy",
            "  Port 2202",
            "  IdentityFile ~/.ssh/prod_ed25519",
            "Host jump?",
            "  HostName ignored"
        ]);

        var entry = Assert.Single(entries);
        Assert.Equal("prod", entry.Alias);
        Assert.Equal("10.0.0.5", entry.HostName);
        Assert.Equal("deploy", entry.User);
        Assert.Equal(2202, entry.Port);
        Assert.EndsWith(Path.Combine(".ssh", "prod_ed25519"), entry.IdentityFile);
    }

    [Fact]
    public void ParseLines_SupportsEqualsSyntax()
    {
        var entries = SshConfigParser.ParseLines([
            "Host=vps",
            "HostName=example.com",
            "User=root",
            "Port=22"
        ]);

        var profile = Assert.Single(entries).ToConnectionProfile();
        Assert.Equal("vps", profile.Label);
        Assert.Equal("example.com", profile.SshHost);
        Assert.Equal("root", profile.SshUser);
        Assert.Equal(22, profile.SshPort);
    }
}
