using HermesDesktop.Models;

namespace HermesDesktop.Services;

public class RemoteScriptException : Exception
{
    public string ScriptName { get; }
    public int ExitCode { get; }
    public string Stderr { get; }

    public RemoteScriptException(string scriptName, int exitCode, string stderr)
        : base($"Remote script '{scriptName}' failed (exit {exitCode}): {stderr}")
    {
        ScriptName = scriptName;
        ExitCode = exitCode;
        Stderr = stderr;
    }
}

public interface IRemoteScriptExecutor
{
    Task<T> ExecuteAsync<T>(
        ConnectionProfile profile,
        string scriptName,
        Dictionary<string, object>? parameters = null,
        CancellationToken ct = default) where T : class;

    Task<string> ExecuteRawAsync(
        ConnectionProfile profile,
        string scriptName,
        Dictionary<string, object>? parameters = null,
        CancellationToken ct = default);
}
