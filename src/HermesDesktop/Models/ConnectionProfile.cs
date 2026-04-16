using System.Text.Json.Serialization;

namespace HermesDesktop.Models;

public class ConnectionProfile
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("sshHost")]
    public string SshHost { get; set; } = string.Empty;

    [JsonPropertyName("sshUser")]
    public string SshUser { get; set; } = string.Empty;

    [JsonPropertyName("sshPort")]
    public int SshPort { get; set; } = 22;

    [JsonPropertyName("sshKeyPath")]
    public string? SshKeyPath { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Label) &&
        !string.IsNullOrWhiteSpace(SshHost) &&
        !string.IsNullOrWhiteSpace(SshUser);

    public string DisplayTarget => $"{SshUser}@{SshHost}:{SshPort}";
}
