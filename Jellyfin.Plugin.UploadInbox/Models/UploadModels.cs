using System;

namespace Jellyfin.Plugin.UploadInbox.Models;

/// <summary>
/// Represents the state of an upload session.
/// </summary>
public enum UploadSessionStatus
{
    Pending,
    InProgress,
    Uploaded,
    Finalised,
    Failed,
    Cancelled,
}

/// <summary>
/// Metadata for an upload session.
/// </summary>
public class UploadSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public Guid UserId { get; set; }

    public string TargetId { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string SanitizedFileName { get; set; } = string.Empty;

    public string TempFilePath { get; set; } = string.Empty;

    public string FinalFilePath { get; set; } = string.Empty;

    public long TotalBytes { get; set; }

    public long ReceivedBytes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public UploadSessionStatus Status { get; set; } = UploadSessionStatus.Pending;
}

/// <summary>
/// Create upload session request.
/// </summary>
public class CreateUploadSessionRequest
{
    public string TargetId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long TotalBytes { get; set; }

    public string? ContentType { get; set; }
}

/// <summary>
/// Create upload session response.
/// </summary>
public class CreateUploadSessionResponse
{
    public string UploadId { get; set; } = string.Empty;

    public long ChunkSize { get; set; }

    public long MaxFileSizeBytes { get; set; }

    public long ReceivedBytes { get; set; }
}

/// <summary>
/// Upload status response for resume.
/// </summary>
public class UploadStatusResponse
{
    public string UploadId { get; set; } = string.Empty;

    public UploadSessionStatus Status { get; set; }

    public long TotalBytes { get; set; }

    public long ReceivedBytes { get; set; }
}

