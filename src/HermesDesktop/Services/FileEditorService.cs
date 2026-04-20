using System.Text.Json;
using System.Text.Json.Serialization;
using HermesDesktop.Models;
using Microsoft.Extensions.Logging;

namespace HermesDesktop.Services;

public class FileEditorService : IFileEditorService
{
    private readonly IRemoteScriptExecutor _executor;
    private readonly ILogger<FileEditorService> _logger;

    public FileEditorService(IRemoteScriptExecutor executor, ILogger<FileEditorService> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    public async Task<RemoteFileDocument> LoadFileAsync(
        ConnectionProfile profile, string remotePath, CancellationToken ct)
    {
        var result = await _executor.ExecuteAsync<ReadFileResponse>(
            profile, "read_file.py",
            new() { ["path"] = remotePath }, ct);

        if (!result.Ok)
            throw new InvalidOperationException(result.Error ?? "读取文件失败");

        return new RemoteFileDocument(
            remotePath,
            result.Content ?? string.Empty,
            result.ContentHash ?? string.Empty,
            DateTime.UtcNow);
    }

    public async Task<FileSaveResult> SaveFileAsync(
        ConnectionProfile profile, RemoteFileDocument document, string newContent, CancellationToken ct)
    {
        var result = await _executor.ExecuteAsync<WriteFileResponse>(
            profile, "write_file.py",
            new()
            {
                ["path"] = document.RemotePath,
                ["content"] = newContent,
                ["expected_content_hash"] = document.ContentHash
            }, ct);

        if (!result.Ok)
        {
            return new FileSaveResult(false, result.Error, null);
        }

        var updatedDoc = new RemoteFileDocument(
            document.RemotePath,
            newContent,
            result.ContentHash ?? string.Empty,
            DateTime.UtcNow);

        return new FileSaveResult(true, null, updatedDoc);
    }

    private class ReadFileResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("content_hash")]
        public string? ContentHash { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    private class WriteFileResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("content_hash")]
        public string? ContentHash { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
