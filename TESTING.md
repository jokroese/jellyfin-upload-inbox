# Testing

## Unit tests

Fast, no Docker required.

```bash
dotnet test Jellyfin.Plugin.UploadInbox.Tests/Jellyfin.Plugin.UploadInbox.Tests.csproj
```

## Integration tests

Integration tests spin up a real Jellyfin instance in Docker using
[Testcontainers for .NET](https://dotnet.testcontainers.org/), install the plugin, configure it via the API,
upload a file through the plugin endpoints, and assert the file appears in the mounted inbox folder.

### Prerequisites

- Docker running locally (Docker Desktop on macOS/Windows, or Docker Engine on Linux)
- Plugin must be published before running the tests

### Run

```bash
# 1. Publish the plugin (must be done first)
dotnet publish Jellyfin.Plugin.UploadInbox/Jellyfin.Plugin.UploadInbox.csproj -c Release

# 2. Run integration tests
dotnet test Jellyfin.Plugin.UploadInbox.IntegrationTests/Jellyfin.Plugin.UploadInbox.IntegrationTests.csproj
```

### How the tests work

1. `JellyfinFixture` (`IAsyncLifetime`) creates two temporary directories on the host:
   - `jf-config-<guid>/` — Jellyfin's `/config` volume (plugin is copied here)
   - `jf-inbox-<guid>/` — mounted as `/inbox` inside the container
2. Publishes plugin files into `jf-config-<guid>/plugins/UploadInbox/`
3. Starts `jellyfin/jellyfin:latest` in Docker with those mounts, exposing port 8096 on a random host port
4. Polls `GET /System/Info/Public` until Jellyfin is ready (up to 3 minutes)
5. Completes the Jellyfin startup wizard headlessly via the `/Startup/*` API endpoints
6. Authenticates as the generated admin user to obtain an access token
7. Each test configures a plugin target via `POST /Plugins/{id}/Configuration`
8. Creates an upload session, uploads chunk(s), finalises — then asserts the file exists in the host-side inbox dir

### Plugin publish directory

The fixture auto-discovers the publish directory by walking up from the test assembly to the repo root and
constructing `Jellyfin.Plugin.UploadInbox/bin/Release/net9.0/publish/`. You can override this with the
`PLUGIN_PUBLISH_DIR` environment variable.

### Common failures

| Symptom | Cause | Fix |
|---------|-------|-----|
| `DirectoryNotFoundException: Plugin publish directory not found` | Plugin not built | Run `dotnet publish` first (see step 1 above) |
| `TimeoutException: Jellyfin did not become ready` | Docker not running, or image pull slow | Ensure Docker is running; retry after pull completes |
| Port conflict errors | Another process on 8096 | Testcontainers maps to a random port; this shouldn't happen — check Docker state |
| 403 on upload | Target not configured, or user ID mismatch | Check the fixture sets up AllowedUserIds correctly |

## CI

GitHub Actions runs both suites on every push and PR (see `.github/workflows/ci.yml`).
Integration tests run on the `ubuntu-latest` runner which includes Docker.
