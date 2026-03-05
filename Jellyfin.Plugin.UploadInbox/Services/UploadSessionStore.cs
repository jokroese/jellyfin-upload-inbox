using System;
using System.Collections.Concurrent;
using Jellyfin.Plugin.UploadInbox.Configuration;
using Jellyfin.Plugin.UploadInbox.Models;

namespace Jellyfin.Plugin.UploadInbox.Services;

/// <summary>
/// In-memory store for upload sessions.
/// </summary>
public class UploadSessionStore
{
    private readonly ConcurrentDictionary<string, UploadSession> _sessions = new();

    private readonly FilePathPolicy _filePathPolicy;

    public UploadSessionStore(FilePathPolicy filePathPolicy)
    {
        _filePathPolicy = filePathPolicy;
    }

    public UploadSession CreateSession(
        Guid userId,
        UploadTarget target,
        string fileName,
        long totalBytes,
        string? contentType,
        long maxFileSizeBytes,
        long recommendedChunkSizeBytes)
    {
        if (totalBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalBytes));
        }

        if (maxFileSizeBytes > 0 && totalBytes > maxFileSizeBytes)
        {
            throw new InvalidOperationException("File exceeds the configured maximum size.");
        }

        var session = new UploadSession
        {
            UserId = userId,
            TargetId = target.Id,
            OriginalFileName = fileName,
            TotalBytes = totalBytes,
            Status = UploadSessionStatus.Pending,
        };

        var (tempPath, finalPath, sanitizedName) = _filePathPolicy.CreatePaths(target, userId, fileName, session.Id);
        session.TempFilePath = tempPath;
        session.FinalFilePath = finalPath;
        session.SanitizedFileName = sanitizedName;

        _sessions[session.Id] = session;

        return session;
    }

    public bool TryGetSession(string id, out UploadSession? session)
    {
        if (_sessions.TryGetValue(id, out var existing))
        {
            session = existing;
            return true;
        }

        session = null;
        return false;
    }

    public void UpdateProgress(UploadSession session, long receivedBytes)
    {
        session.ReceivedBytes = receivedBytes;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        session.Status = receivedBytes >= session.TotalBytes
            ? UploadSessionStatus.Uploaded
            : UploadSessionStatus.InProgress;
    }

    public void Complete(UploadSession session)
    {
        session.Status = UploadSessionStatus.Finalised;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        _sessions.TryRemove(session.Id, out _);
    }

    public void Fail(UploadSession session)
    {
        session.Status = UploadSessionStatus.Failed;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        _sessions.TryRemove(session.Id, out _);
    }
}

