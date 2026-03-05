using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.UploadInbox.Configuration;
using Jellyfin.Plugin.UploadInbox.Models;
using Jellyfin.Plugin.UploadInbox.Services;
using Jellyfin.Plugin.UploadInbox.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.UploadInbox.Api;

/// <summary>
/// Upload inbox API.
/// </summary>
[ApiController]
[Authorize]
[Route("uploadinbox")]
public class UploadController : ControllerBase
{
    private const long DefaultChunkSizeBytes = 8L * 1024 * 1024;
    private const long MaxChunkSizeBytes = 64L * 1024 * 1024;

    private readonly UploadAuthoriser _uploadAuthoriser;
    private readonly UploadSessionStore _sessionStore;

    public UploadController(UploadAuthoriser uploadAuthoriser, UploadSessionStore sessionStore)
    {
        _uploadAuthoriser = uploadAuthoriser;
        _sessionStore = sessionStore;
    }

    /// <summary>
    /// Creates a new upload session.
    /// </summary>
    [HttpPost("uploads")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<CreateUploadSessionResponse> CreateUpload(
        [FromBody] CreateUploadSessionRequest request)
    {
        if (request is null)
        {
            return BadRequest("Missing request body.");
        }

        if (string.IsNullOrWhiteSpace(request.TargetId))
        {
            return BadRequest("TargetId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return BadRequest("FileName is required.");
        }

        if (request.TotalBytes <= 0)
        {
            return BadRequest("TotalBytes must be positive.");
        }

        var userId = User.GetJellyfinUserId();
        var configuration = Plugin.Instance?.Configuration;
        if (configuration is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "Plugin configuration is not available.");
        }

        if (!_uploadAuthoriser.TryEnsureAllowed(configuration, userId, request.TargetId, out var target) ||
            target is null)
        {
            return Forbid();
        }

        var maxFileSize = target.MaxFileSizeBytes > 0
            ? target.MaxFileSizeBytes
            : long.MaxValue;

        if (request.TotalBytes > maxFileSize)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, "File exceeds maximum configured size.");
        }

        try
        {
            var session = _sessionStore.CreateSession(
                userId,
                target,
                request.FileName,
                request.TotalBytes,
                request.ContentType,
                maxFileSize,
                DefaultChunkSizeBytes);

            var response = new CreateUploadSessionResponse
            {
                UploadId = session.Id,
                ChunkSize = DefaultChunkSizeBytes,
                MaxFileSizeBytes = maxFileSize,
                ReceivedBytes = session.ReceivedBytes,
            };

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (IOException ex)
        {
            return StatusCode(StatusCodes.Status507InsufficientStorage, ex.Message);
        }
    }

    /// <summary>
    /// Uploads a single chunk of an upload session.
    /// </summary>
    [HttpPatch("uploads/{uploadId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status507InsufficientStorage)]
    public async Task<ActionResult<object>> UploadChunk(
        [FromRoute] string uploadId,
        CancellationToken cancellationToken)
    {
        if (!_sessionStore.TryGetSession(uploadId, out var session) || session is null)
        {
            return NotFound();
        }

        var userId = User.GetJellyfinUserId();
        if (session.UserId != userId)
        {
            return Forbid();
        }

        if (!Request.Headers.TryGetValue("Content-Range", out var contentRangeValues))
        {
            return BadRequest("Missing Content-Range header.");
        }

        if (!TryParseContentRange(contentRangeValues.ToString(), out var start, out var endInclusive, out var total))
        {
            return BadRequest("Invalid Content-Range header.");
        }

        if (total != session.TotalBytes)
        {
            return BadRequest("Total size does not match session.");
        }

        if (start != session.ReceivedBytes)
        {
            return BadRequest("Chunk start does not match expected offset.");
        }

        var bytesToWrite = endInclusive - start + 1;
        if (bytesToWrite <= 0)
        {
            return BadRequest("Invalid chunk size.");
        }

        if (bytesToWrite > MaxChunkSizeBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, "Chunk size exceeds server limit.");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(session.TempFilePath) ?? ".");

            await using (var stream = new FileStream(
                             session.TempFilePath,
                             FileMode.OpenOrCreate,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             useAsync: true))
            {
                stream.Seek(start, SeekOrigin.Begin);

                if (Request.ContentLength is long contentLength && contentLength != bytesToWrite)
                {
                    return BadRequest("Content-Length does not match Content-Range.");
                }

                var remaining = bytesToWrite;
                var buffer = new byte[Math.Min(81920, (int)Math.Min(bytesToWrite, int.MaxValue))];

                while (remaining > 0)
                {
                    var toRead = (int)Math.Min(buffer.Length, remaining);
                    var read = await Request.Body.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        return BadRequest("Unexpected end of request body.");
                    }

                    await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    remaining -= read;
                }
            }

            var newReceived = endInclusive + 1;
            _sessionStore.UpdateProgress(session, newReceived);

            return Ok(new { receivedBytes = session.ReceivedBytes });
        }
        catch (IOException ex)
        {
            _sessionStore.Fail(session);
            return StatusCode(StatusCodes.Status507InsufficientStorage, ex.Message);
        }
    }

    /// <summary>
    /// Finalises an upload and moves the file into place.
    /// </summary>
    [HttpPost("uploads/{uploadId}/finalise")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status507InsufficientStorage)]
    public ActionResult<object> FinaliseUpload([FromRoute] string uploadId)
    {
        if (!_sessionStore.TryGetSession(uploadId, out var session) || session is null)
        {
            return NotFound();
        }

        var userId = User.GetJellyfinUserId();
        if (session.UserId != userId)
        {
            return Forbid();
        }

        if (session.ReceivedBytes != session.TotalBytes)
        {
            return BadRequest("Upload is not complete.");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(session.FinalFilePath) ?? ".");

            if (!System.IO.File.Exists(session.TempFilePath))
            {
                return BadRequest("Temporary upload file is missing.");
            }

            System.IO.File.Move(
                session.TempFilePath,
                session.FinalFilePath,
                overwrite: false);

            _sessionStore.Complete(session);

            return Ok(new { storedFileName = Path.GetFileName(session.FinalFilePath) });
        }
        catch (IOException ex)
        {
            _sessionStore.Fail(session);
            return StatusCode(StatusCodes.Status507InsufficientStorage, ex.Message);
        }
    }

    /// <summary>
    /// Gets the status of an upload session.
    /// </summary>
    [HttpGet("uploads/{uploadId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<UploadStatusResponse> GetStatus([FromRoute] string uploadId)
    {
        if (!_sessionStore.TryGetSession(uploadId, out var session) || session is null)
        {
            return NotFound();
        }

        var userId = User.GetJellyfinUserId();
        if (session.UserId != userId)
        {
            return Forbid();
        }

        var response = new UploadStatusResponse
        {
            UploadId = session.Id,
            Status = session.Status,
            TotalBytes = session.TotalBytes,
            ReceivedBytes = session.ReceivedBytes,
        };

        return Ok(response);
    }

    private static bool TryParseContentRange(string value, out long start, out long endInclusive, out long total)
    {
        start = 0;
        endInclusive = 0;
        total = 0;

        // Expected format: bytes start-end/total
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(' ', 2);
        if (parts.Length != 2 || !string.Equals(parts[0], "bytes", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rangeAndTotal = parts[1].Split('/', 2);
        if (rangeAndTotal.Length != 2)
        {
            return false;
        }

        var range = rangeAndTotal[0].Split('-', 2);
        if (range.Length != 2)
        {
            return false;
        }

        if (!long.TryParse(range[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out start) ||
            !long.TryParse(range[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out endInclusive) ||
            !long.TryParse(rangeAndTotal[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out total))
        {
            return false;
        }

        return start >= 0 && endInclusive >= start && total > 0;
    }
}

