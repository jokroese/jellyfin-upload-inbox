using System.Net;
using Xunit;

namespace Jellyfin.Plugin.UploadInbox.IntegrationTests;

/// <summary>
/// End-to-end integration test: configures the plugin via the Jellyfin API, uploads a file
/// in chunks, finalises the upload, and asserts the file is present in the mounted inbox folder.
/// </summary>
[Collection("Jellyfin")]
public sealed class UploadEndToEndTest : IClassFixture<JellyfinFixture>
{
    private static readonly Guid PluginId = Guid.Parse("b3ff3bcd-9b77-4a5e-9c22-3c5236757d12");

    private readonly JellyfinFixture _fx;

    public UploadEndToEndTest(JellyfinFixture fixture)
    {
        _fx = fixture;
    }

    [Fact(Timeout = 300_000)]
    public async Task Upload_SmallFile_AppearsInInbox()
    {
        // ── Configure plugin with one target ─────────────────────────────────
        var targetId = Guid.NewGuid().ToString("N");

        await _fx.Client.UpdatePluginConfigurationAsync(PluginId, new
        {
            Targets = new[]
            {
                new
                {
                    Id = targetId,
                    DisplayName = "Test Inbox",
                    BasePath = "/inbox",
                    CreateUserSubfolder = false,
                    MaxFileSizeBytes = 10 * 1024 * 1024L,
                    AllowedExtensions = Array.Empty<string>(),
                    AllowedUserIds = new[] { _fx.AdminUserId.ToString() },
                },
            },
        });

        // ── Prepare file content ──────────────────────────────────────────────
        var content = System.Text.Encoding.UTF8.GetBytes("Hello from integration test!");
        var fileName = $"test-{Guid.NewGuid():N}.txt";

        // ── Create upload session ─────────────────────────────────────────────
        var session = await _fx.Client.CreateUploadSessionAsync(targetId, fileName, content.Length);

        Assert.False(string.IsNullOrEmpty(session.UploadId));
        Assert.True(session.ChunkSize > 0);

        // ── Upload single chunk ───────────────────────────────────────────────
        await _fx.Client.UploadChunkAsync(
            session.UploadId,
            start: 0,
            endInclusive: content.Length - 1,
            total: content.Length,
            data: content);

        // ── Finalise ──────────────────────────────────────────────────────────
        var result = await _fx.Client.FinaliseAsync(session.UploadId);

        Assert.False(string.IsNullOrEmpty(result.StoredFileName));

        // ── Assert file is on the host filesystem ─────────────────────────────
        var storedPath = Path.Combine(_fx.InboxDir, result.StoredFileName);
        Assert.True(File.Exists(storedPath), $"Expected file at: {storedPath}");

        var stored = await File.ReadAllBytesAsync(storedPath);
        Assert.Equal(content, stored);
    }

    [Fact(Timeout = 300_000)]
    public async Task Upload_MultiChunk_AppearsInInbox()
    {
        // ── Configure plugin ──────────────────────────────────────────────────
        var targetId = Guid.NewGuid().ToString("N");

        await _fx.Client.UpdatePluginConfigurationAsync(PluginId, new
        {
            Targets = new[]
            {
                new
                {
                    Id = targetId,
                    DisplayName = "Test Inbox Multi",
                    BasePath = "/inbox",
                    CreateUserSubfolder = false,
                    MaxFileSizeBytes = 10 * 1024 * 1024L,
                    AllowedExtensions = Array.Empty<string>(),
                    AllowedUserIds = new[] { _fx.AdminUserId.ToString() },
                },
            },
        });

        // ── Build a 3-chunk payload ────────────────────────────────────────────
        // Use a chunk size smaller than the default so we exercise the loop.
        const int chunkSize = 10;
        var content = System.Text.Encoding.UTF8.GetBytes("AAAAAAAAAA" + "BBBBBBBBBB" + "CCCCCCCCCC"); // 30 bytes
        var fileName = $"multi-{Guid.NewGuid():N}.txt";

        var session = await _fx.Client.CreateUploadSessionAsync(targetId, fileName, content.Length);

        // Upload in 3 chunks of 10 bytes each (override the session's chunk size for this test).
        for (int offset = 0; offset < content.Length; offset += chunkSize)
        {
            var end = Math.Min(offset + chunkSize, content.Length);
            var chunk = content[offset..end];
            await _fx.Client.UploadChunkAsync(
                session.UploadId,
                start: offset,
                endInclusive: end - 1,
                total: content.Length,
                data: chunk);
        }

        var result = await _fx.Client.FinaliseAsync(session.UploadId);

        var storedPath = Path.Combine(_fx.InboxDir, result.StoredFileName);
        Assert.True(File.Exists(storedPath), $"Expected file at: {storedPath}");

        var stored = await File.ReadAllBytesAsync(storedPath);
        Assert.Equal(content, stored);
    }

    [Fact(Timeout = 300_000)]
    public async Task Upload_ForbiddenWhenUserNotAllowed_Returns403()
    {
        // Configure target with empty AllowedUserIds so the authenticated admin is not allowed.
        var targetId = Guid.NewGuid().ToString("N");

        await _fx.Client.UpdatePluginConfigurationAsync(PluginId, new
        {
            Targets = new[]
            {
                new
                {
                    Id = targetId,
                    DisplayName = "Forbidden Inbox",
                    BasePath = "/inbox",
                    CreateUserSubfolder = false,
                    MaxFileSizeBytes = 10 * 1024 * 1024L,
                    AllowedExtensions = Array.Empty<string>(),
                    AllowedUserIds = Array.Empty<string>(),
                },
            },
        });

        var content = System.Text.Encoding.UTF8.GetBytes("forbidden");
        var fileName = $"forbidden-{Guid.NewGuid():N}.txt";

        using var response = await _fx.Client.CreateUploadSessionResponseAsync(targetId, fileName, content.Length);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
