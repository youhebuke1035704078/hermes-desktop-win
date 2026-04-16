using System.Text.Json.Serialization;

namespace HermesDesktop.Models;

public class AppPreferences
{
    [JsonPropertyName("lastConnectionId")]
    public Guid? LastConnectionId { get; set; }
}
