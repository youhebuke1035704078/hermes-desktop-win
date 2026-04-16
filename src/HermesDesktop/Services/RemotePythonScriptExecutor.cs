using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using HermesDesktop.Models;
using Microsoft.Extensions.Logging;

namespace HermesDesktop.Services;

public class RemotePythonScriptExecutor : IRemoteScriptExecutor
{
    private readonly ISshTransport _ssh;
    private readonly ILogger<RemotePythonScriptExecutor> _logger;
    private readonly Dictionary<string, string> _scriptCache = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RemotePythonScriptExecutor(ISshTransport ssh, ILogger<RemotePythonScriptExecutor> logger)
    {
        _ssh = ssh;
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(
        ConnectionProfile profile,
        string scriptName,
        Dictionary<string, object>? parameters,
        CancellationToken ct) where T : class
    {
        var json = await ExecuteRawAsync(profile, scriptName, parameters, ct);
        return JsonSerializer.Deserialize<T>(json, _jsonOptions)
            ?? throw new InvalidOperationException($"Script '{scriptName}' returned null JSON");
    }

    public async Task<string> ExecuteRawAsync(
        ConnectionProfile profile,
        string scriptName,
        Dictionary<string, object>? parameters,
        CancellationToken ct)
    {
        var script = LoadScript(scriptName);

        // Inject parameters as a `payload` global variable (matches macOS app pattern)
        if (parameters is { Count: > 0 })
        {
            var paramsJson = JsonSerializer.Serialize(parameters);
            script = $"import json as _json\npayload = _json.loads({PythonStringLiteral(paramsJson)})\n" + script;
        }
        else
        {
            script = "payload = {}\n" + script;
        }

        var scriptBytes = Encoding.UTF8.GetBytes(script);
        var base64Script = Convert.ToBase64String(scriptBytes);

        // Build the remote command: decode base64 and pipe to python3
        var command = $"printf '%s' '{base64Script}' | base64 -d | python3 -";

        _logger.LogDebug("Executing remote script: {Script}", scriptName);

        var result = await _ssh.ExecuteCommandAsync(
            profile, command, ct, TimeSpan.FromSeconds(60));

        if (result.ExitCode != 0)
        {
            _logger.LogError("Python script {Script} failed (exit {Code}): {Error}",
                scriptName, result.ExitCode, result.StandardError);
            throw new RemoteScriptException(scriptName, result.ExitCode, result.StandardError);
        }

        return result.StandardOutput;
    }

    private static string PythonStringLiteral(string value)
    {
        // Use triple-quoted raw string to safely embed JSON
        var escaped = value.Replace("\\", "\\\\").Replace("'", "\\'");
        return $"'{escaped}'";
    }

    private string LoadScript(string scriptName)
    {
        if (_scriptCache.TryGetValue(scriptName, out var cached))
            return cached;

        var assembly = typeof(RemotePythonScriptExecutor).Assembly;
        var resourceName = $"HermesDesktop.Scripts.{scriptName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded script not found: {resourceName}");
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        _scriptCache[scriptName] = content;
        return content;
    }
}
