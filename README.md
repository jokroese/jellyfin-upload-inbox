# Jellyfin Upload Inbox

A Jellyfin plugin that lets authenticated users upload files directly to a configured "inbox"
folder on the server — no SSH, no shared drives. Uploads are chunked (resumable) and authorised
per-user via the plugin configuration page.

## Repository layout

- `Jellyfin.Plugin.UploadInbox/` — plugin source
- `Jellyfin.Plugin.UploadInbox.Tests/` — tests
- `dev/` — local Jellyfin environment (Docker Compose) for fast iteration

## Quickstart: local development

### 1) Start the local Jellyfin dev server

```bash
cp dev/.env.example dev/.env
mkdir -p dev/jf-config dev/jf-cache dev/inbox
cd dev
docker compose --env-file .env up -d
```

Open Jellyfin at `http://localhost:8096` and complete the initial setup wizard.

### 2) Build → install → restart (the dev loop)

From the repo root:

```bash
dotnet clean
dotnet publish Jellyfin.Plugin.UploadInbox/Jellyfin.Plugin.UploadInbox.csproj -c Release
./dev/scripts/install-plugin.sh
(cd dev && docker compose restart jellyfin)
```

That copies the published plugin output into the dev Jellyfin config mount at:

`dev/jf-config/plugins/UploadInbox/`

### 3) Configure the plugin

In Jellyfin:

- Dashboard → Plugins → **Upload Inbox** → Settings
- Add at least one target:
    - **Base path (on server)**: `/inbox` (mounted from `dev/inbox`)
    - **Who can upload**: default is **All users**

## Testing

```bash
# Unit tests (no Docker)
dotnet test Jellyfin.Plugin.UploadInbox.Tests/Jellyfin.Plugin.UploadInbox.Tests.csproj

# Integration tests (requires Docker)
dotnet publish Jellyfin.Plugin.UploadInbox/Jellyfin.Plugin.UploadInbox.csproj -c Release
dotnet test Jellyfin.Plugin.UploadInbox.IntegrationTests/Jellyfin.Plugin.UploadInbox.IntegrationTests.csproj
```

See [TESTING.md](TESTING.md) for details on how the integration tests work and common failures.

## More detailed dev environment notes

See `dev/README.md`.
