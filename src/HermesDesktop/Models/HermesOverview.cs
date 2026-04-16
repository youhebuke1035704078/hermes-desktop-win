using System.Text.Json.Serialization;

namespace HermesDesktop.Models;

public class HermesOverview
{
    [JsonPropertyName("home")]
    public string Home { get; set; } = string.Empty;

    [JsonPropertyName("hermes_root")]
    public string HermesRoot { get; set; } = string.Empty;

    [JsonPropertyName("session_source")]
    public string? SessionSource { get; set; }

    [JsonPropertyName("session_store")]
    public string? SessionStore { get; set; }

    [JsonPropertyName("tracked_files")]
    public List<TrackedFile> TrackedFiles { get; set; } = new();

    [JsonPropertyName("python_version")]
    public string? PythonVersion { get; set; }
}

public class TrackedFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("exists")]
    public bool Exists { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }
}
