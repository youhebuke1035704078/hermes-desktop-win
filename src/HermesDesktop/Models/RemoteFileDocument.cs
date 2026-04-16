namespace HermesDesktop.Models;

public record RemoteFileDocument(
    string RemotePath,
    string Content,
    string ContentHash,
    DateTime LoadedAt);

public record FileSaveResult(
    bool Success,
    string? ConflictMessage,
    RemoteFileDocument? UpdatedDocument);
