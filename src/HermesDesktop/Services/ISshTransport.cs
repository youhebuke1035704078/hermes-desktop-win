using HermesDesktop.Models;
using Renci.SshNet;

namespace HermesDesktop.Services;

public record SshCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration);

public record ShellStreamSession(ShellStream Stream, SshClient Client);

public class SshConnectionEventArgs : EventArgs
{
    public Guid ProfileId { get; init; }
    public SshConnectionState State { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum SshConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

public interface ISshTransport
{
    Task<SshCommandResult> ExecuteCommandAsync(
        ConnectionProfile profile,
        string command,
        CancellationToken ct = default,
        TimeSpan? timeout = null);

    Task<ShellStreamSession> OpenShellAsync(
        ConnectionProfile profile,
        int columns, int rows,
        CancellationToken ct = default);

    Task DisconnectAsync(Guid profileId);
    Task DisconnectAllAsync();

    event EventHandler<SshConnectionEventArgs>? ConnectionStateChanged;
}
