using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.UploadInbox.IntegrationTests;

/// <summary>
/// Minimal HTTP client for the Jellyfin API used by integration tests.
/// </summary>
public sealed class JellyfinClient : IDisposable
{
    private const string DeviceInfo =
        "Client=\"IntegrationTest\", Device=\"IntegrationTest\", DeviceId=\"integration-test-1\", Version=\"1.0.0\"";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private string? _token;

    public string? UserId { get; private set; }

    public JellyfinClient(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    // ── Startup wizard ────────────────────────────────────────────────────────

    public async Task CompleteStartupWizardAsync(string username, string password)
    {
        await SendVoidAsync(HttpMethod.Post, "/Startup/Configuration", new
        {
            UICulture = "en-US",
            MetadataCountryCode = "US",
            PreferredMetadataLanguage = "en",
        });

        await SendVoidAsync(HttpMethod.Post, "/Startup/User", new
        {
            Name = username,
            Password = password,
        });

        await SendVoidAsync(HttpMethod.Post, "/Startup/Complete");
    }

    // ── Authentication ────────────────────────────────────────────────────────

    public async Task AuthenticateAsync(string username, string password)
    {
        var node = await SendAsync<JsonObject>(HttpMethod.Post, "/Users/AuthenticateByName", new
        {
            Username = username,
            Pw = password,
        });

        _token = node["AccessToken"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Auth response missing AccessToken.");

        UserId = node["User"]?["Id"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Auth response missing User.Id.");
    }

    // ── Plugin configuration ──────────────────────────────────────────────────

    public Task UpdatePluginConfigurationAsync(Guid pluginId, object config)
        => SendVoidAsync(HttpMethod.Post, $"/Plugins/{pluginId}/Configuration", config);

    // ── Upload API ────────────────────────────────────────────────────────────

    public Task<CreateSessionResult> CreateUploadSessionAsync(
        string targetId, string fileName, long totalBytes)
        => SendAsync<CreateSessionResult>(HttpMethod.Post, "/uploadinbox/uploads", new
        {
            targetId,
            fileName,
            totalBytes,
            contentType = (string?)null,
        });

    public async Task UploadChunkAsync(
        string uploadId, long start, long endInclusive, long total, byte[] data)
    {
        using var req = new HttpRequestMessage(
            new HttpMethod("PATCH"),
            $"/uploadinbox/uploads/{Uri.EscapeDataString(uploadId)}");

        AddAuthHeader(req);
        req.Headers.TryAddWithoutValidation(
            "Content-Range", $"bytes {start}-{endInclusive}/{total}");
        req.Content = new ByteArrayContent(data);

        using var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    public Task<FinaliseResult> FinaliseAsync(string uploadId)
        => SendAsync<FinaliseResult>(
            HttpMethod.Post,
            $"/uploadinbox/uploads/{Uri.EscapeDataString(uploadId)}/finalise");

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null)
    {
        using var req = BuildRequest(method, path, body);
        using var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var text = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(text, JsonOptions)
            ?? throw new InvalidOperationException($"Null response from {path}");
    }

    private async Task SendVoidAsync(HttpMethod method, string path, object? body = null)
    {
        using var req = BuildRequest(method, path, body);
        using var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, object? body)
    {
        var req = new HttpRequestMessage(method, path);
        AddAuthHeader(req);
        if (body != null)
        {
            req.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");
        }

        return req;
    }

    private void AddAuthHeader(HttpRequestMessage req)
    {
        var header = _token != null
            ? $"MediaBrowser {DeviceInfo}, Token=\"{_token}\""
            : $"MediaBrowser {DeviceInfo}";
        req.Headers.TryAddWithoutValidation("Authorization", header);
    }

    public void Dispose() => _http.Dispose();
}

// ── Result types ──────────────────────────────────────────────────────────────

public sealed class CreateSessionResult
{
    [JsonPropertyName("uploadId")]
    public string UploadId { get; init; } = string.Empty;

    [JsonPropertyName("chunkSize")]
    public long ChunkSize { get; init; }

    [JsonPropertyName("maxFileSizeBytes")]
    public long MaxFileSizeBytes { get; init; }

    [JsonPropertyName("receivedBytes")]
    public long ReceivedBytes { get; init; }
}

public sealed class FinaliseResult
{
    [JsonPropertyName("storedFileName")]
    public string StoredFileName { get; init; } = string.Empty;
}
