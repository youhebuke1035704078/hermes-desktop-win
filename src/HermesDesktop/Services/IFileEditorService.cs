using HermesDesktop.Models;

namespace HermesDesktop.Services;

public interface IFileEditorService
{
    Task<RemoteFileDocument> LoadFileAsync(ConnectionProfile profile, string remotePath, CancellationToken ct = default);
    Task<FileSaveResult> SaveFileAsync(ConnectionProfile profile, RemoteFileDocument document, string newContent, CancellationToken ct = default);
}
