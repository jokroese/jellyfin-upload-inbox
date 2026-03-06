using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Jellyfin.Plugin.UploadInbox.IntegrationTests;

/// <summary>
/// xUnit collection fixture: starts a real Jellyfin container with the plugin installed,
/// completes the startup wizard headlessly, and exposes an authenticated <see cref="JellyfinClient"/>.
/// </summary>
public sealed class JellyfinFixture : IAsyncLifetime
{
    private const string JellyfinImage = "jellyfin/jellyfin:10.11.6";
    private const int JellyfinPort = 8096;

    // Credentials generated fresh for each test run.
    private const string AdminUsername = "testadmin";
    private readonly string _adminPassword = "Test_" + Guid.NewGuid().ToString("N")[..10] + "_1A!";

    private IContainer? _container;
    private string? _configDir;

    public string InboxDir { get; private set; } = string.Empty;
    public JellyfinClient Client { get; private set; } = null!;
    public Guid AdminUserId { get; private set; }

    // ── IAsyncLifetime ────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        _configDir = CreateTempDir("jf-config");
        InboxDir = CreateTempDir("jf-inbox");

        CopyPluginIntoConfig(_configDir);

        _container = new ContainerBuilder()
            .WithImage(JellyfinImage)
            .WithPortBinding(JellyfinPort, assignRandomHostPort: true)
            .WithBindMount(_configDir, "/config")
            .WithBindMount(InboxDir, "/inbox")
            .WithEnvironment("JELLYFIN_PublishedServerUrl", "http://localhost")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(JellyfinPort))
            .Build();

        await _container.StartAsync();

        var port = _container.GetMappedPublicPort(JellyfinPort);
        var baseUrl = $"http://localhost:{port}";

        Client = new JellyfinClient(baseUrl);

        // Wait for basic HTTP (the server process is up)
        await WaitForHttpReadyAsync(baseUrl + "/System/Info/Public", TimeSpan.FromMinutes(3));

        // Wait for the startup wizard controller specifically — it initialises later than /System/Info/Public
        await WaitForHttpReadyAsync(baseUrl + "/Startup/Configuration", TimeSpan.FromMinutes(2));

        await Client.CompleteStartupWizardAsync(AdminUsername, _adminPassword);

        // Brief pause — Jellyfin may need a moment after wizard completion.
        await Task.Delay(TimeSpan.FromSeconds(2));

        await Client.AuthenticateAsync(AdminUsername, _adminPassword);

        AdminUserId = Guid.Parse(Client.UserId
            ?? throw new InvalidOperationException("No UserId after authentication."));
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();

        if (_container != null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }

        TryDeleteDir(_configDir);
        TryDeleteDir(InboxDir);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string CreateTempDir(string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CopyPluginIntoConfig(string configDir)
    {
        var publishDir = FindPluginPublishDir();
        var pluginDir = Path.Combine(configDir, "plugins", "UploadInbox");
        Directory.CreateDirectory(pluginDir);

        foreach (var file in Directory.EnumerateFiles(publishDir))
        {
            File.Copy(file, Path.Combine(pluginDir, Path.GetFileName(file)));
        }
    }

    private static string FindPluginPublishDir()
    {
        var fromEnv = Environment.GetEnvironmentVariable("PLUGIN_PUBLISH_DIR");
        if (!string.IsNullOrEmpty(fromEnv))
        {
            if (!Directory.Exists(fromEnv))
                throw new DirectoryNotFoundException(
                    $"PLUGIN_PUBLISH_DIR env var points to missing directory: {fromEnv}");
            return fromEnv;
        }

        // Walk up from the test assembly to locate the repo root.
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Jellyfin.Plugin.UploadInbox.slnx")))
            {
                var publishDir = Path.Combine(
                    current.FullName,
                    "Jellyfin.Plugin.UploadInbox",
                    "bin", "Release", "net9.0", "publish");

                if (!Directory.Exists(publishDir))
                    throw new DirectoryNotFoundException(
                        $"Plugin publish directory not found: {publishDir}\n" +
                        "Run: dotnet publish Jellyfin.Plugin.UploadInbox/Jellyfin.Plugin.UploadInbox.csproj -c Release");

                return publishDir;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Cannot locate repo root (no Jellyfin.Plugin.UploadInbox.slnx found in any parent directory).");
    }

    private static async Task WaitForHttpReadyAsync(string url, TimeSpan timeout)
    {
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var resp = await http.GetAsync(url);
                if (resp.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // Not ready yet; keep polling.
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new TimeoutException($"Jellyfin did not become ready within {timeout.TotalMinutes:F1} minutes.");
    }

    private static void TryDeleteDir(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try { Directory.Delete(path, recursive: true); }
        catch { /* best-effort */ }
    }
}
