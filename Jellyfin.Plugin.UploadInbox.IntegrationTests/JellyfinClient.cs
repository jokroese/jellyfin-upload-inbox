using System.Net.Http.Headers;
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
        // 1) Startup configuration
        await SendVoidWithDiagnosticsAsync(HttpMethod.Post, "/Startup/Configuration", new
        {
            UICulture = "en-US",
            MetadataCountryCode = "US",
            PreferredMetadataLanguage = "en",
        }, "Startup/Configuration");

        // 2) PRIME the user manager (important on 10.11.x)
        // This endpoint calls _userManager.InitializeAsync() internally.
        await SendVoidWithDiagnosticsAsync(HttpMethod.Get, "/Startup/User", null, "Startup/User (prime)");

        // 3) Now update the first user
        await SendVoidWithDiagnosticsAsync(HttpMethod.Post, "/Startup/User", new
        {
            Name = username,
            Password = password,
        }, "Startup/User");

        // 4) Complete wizard
        await SendVoidWithDiagnosticsAsync(HttpMethod.Post, "/Startup/Complete", new { }, "Startup/Complete");
    }

    // ── Authentication ────────────────────────────────────────────────────────

    public async Task AuthenticateAsync(string username, string password)
    {
        var (node, _) = await SendWithDiagnosticsAsync<JsonObject>(HttpMethod.Post, "/Users/AuthenticateByName", new
        {
            Username = username,
            Pw = password,
        }, "Users/AuthenticateByName");

        _token = node["AccessToken"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Auth response missing AccessToken.");

        UserId = node["User"]?["Id"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Auth response missing User.Id.");
    }

    // ── Plugin configuration ──────────────────────────────────────────────────

    public async Task UpdatePluginConfigurationAsync(Guid pluginId, object config)
    {
        try
        {
            await SendVoidAsync(HttpMethod.Post, $"/Plugins/{pluginId}/Configuration", config);
        }
        catch
        {
            await SendVoidAsync(HttpMethod.Put, $"/Plugins/{pluginId}/Configuration", config);
        }
    }

    public async Task CreateLibraryAsync(string name, string collectionType, string path)
    {
        var url =
            "/Library/VirtualFolders" +
            $"?name={Uri.EscapeDataString(name)}" +
            $"&collectionType={Uri.EscapeDataString(collectionType)}" +
            $"&paths={Uri.EscapeDataString(path)}" +
            "&refreshLibrary=true";

        await SendVoidAsync(HttpMethod.Post, url);
    }

    public Task<VirtualFolderInfoResult[]> GetVirtualFoldersAsync()
        => SendAsync<VirtualFolderInfoResult[]>(HttpMethod.Get, "/Library/VirtualFolders");

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

    /// <summary>Creates an upload session and returns the raw response (for asserting 403 etc.).</summary>
    public async Task<HttpResponseMessage> CreateUploadSessionResponseAsync(
        string targetId, string fileName, long totalBytes)
    {
        using var req = BuildRequest(HttpMethod.Post, "/uploadinbox/uploads", new
        {
            targetId,
            fileName,
            totalBytes,
            contentType = (string?)null,
        });
        var resp = await _http.SendAsync(req);
        return resp;
    }

    public async Task UploadChunkAsync(
        string uploadId, long start, long endInclusive, long total, byte[] data)
    {
        using var req = new HttpRequestMessage(
            new HttpMethod("PATCH"),
            $"/uploadinbox/uploads/{Uri.EscapeDataString(uploadId)}");

        AddAuthHeader(req);

        // Content + headers (Content-Range is a content header in HttpClient)
        req.Content = new ByteArrayContent(data);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        req.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, endInclusive, total);

        using var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"UploadChunk failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");
        }
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

    private async Task SendVoidWithDiagnosticsAsync(HttpMethod method, string path, object? body, string label)
    {
        using var req = BuildRequest(method, path, body);
        using var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var responseBody = await resp.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"{label} returned {(int)resp.StatusCode}: {responseBody}");
        }
    }

    private async Task<(T Value, string Raw)> SendWithDiagnosticsAsync<T>(HttpMethod method, string path, object? body, string label)
    {
        using var req = BuildRequest(method, path, body);
        using var resp = await _http.SendAsync(req);
        var raw = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"{label} returned {(int)resp.StatusCode}: {raw}");
        var value = JsonSerializer.Deserialize<T>(raw, JsonOptions)
            ?? throw new InvalidOperationException($"Null response from {path}");
        return (value, raw);
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

public sealed class VirtualFolderInfoResult
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    [JsonPropertyName("itemId")]
    public string ItemId { get; init; } = string.Empty;
    [JsonPropertyName("locations")]
    public string[] Locations { get; init; } = Array.Empty<string>();
}
