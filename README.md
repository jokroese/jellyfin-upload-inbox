# Upload Inbox

[![Releases](https://img.shields.io/github/v/release/jokroese/jellyfin-upload-inbox?include_prereleases=&sort=semver)](https://github.com/jokroese/jellyfin-upload-inbox/releases)

Upload files directly to a configured server inbox from Jellyfin. Authenticated users get an **Upload Inbox** page; uploads are chunked (resumable), with per-target permissions, extension allowlists, and file size limits.

## Compatibility

- **Jellyfin:** 10.11.x

## Repository manifest

This project publishes a Jellyfin plugin repository manifest at:

- `https://jokroese.github.io/jellyfin-upload-inbox/manifest.json`

The manifest is generated automatically during tagged releases and follows the modern Jellyfin repository format with top-level plugin metadata and a `versions` array pointing at the released ZIP asset.

---

## Install

### Install from Jellyfin

1. Open Jellyfin.
2. Go to **Dashboard → Plugins → Repositories**.
3. Add this repository URL:

   **https://jokroese.github.io/jellyfin-upload-inbox/manifest.json**

4. Save.
5. Open **Catalog**.
6. Install **Upload Inbox**.
7. Restart Jellyfin.

### Manual installation

Manual install is a fallback for offline or locked-down deployments, not the primary path.

1. Download `UploadInbox_<version>.zip` from [GitHub Releases](https://github.com/jokroese/jellyfin-upload-inbox/releases).
2. Extract it into Jellyfin’s plugin directory (contents at the root of the folder, no extra directory).
3. Restart Jellyfin.

---

## Notes for server admins

- **Reverse proxy:** Uploads may fail unless body-size limits are raised (e.g. `client_max_body_size` in nginx).
- **Filesystem:** Target base paths must be absolute and writable by the Jellyfin process.
- **Docker:** Use paths visible inside the container (e.g. `/inbox`), not host-only paths.

---

## After installation

- **Dashboard → Plugins → Upload Inbox → Settings** to add upload targets (base path, who can upload, extensions, max size).
- The **Upload Inbox** item appears in the main menu for allowed users.

---

## Repository layout (developers)

- `Jellyfin.Plugin.UploadInbox/` — plugin source
- `Jellyfin.Plugin.UploadInbox.Tests/` — unit tests
- `Jellyfin.Plugin.UploadInbox.IntegrationTests/` — integration tests (Docker)
- `dev/` — local Jellyfin (Docker Compose) for development

### Quickstart: local development

```bash
cp dev/.env.example dev/.env
mkdir -p dev/jf-config dev/jf-cache dev/inbox
cd dev && docker compose --env-file .env up -d
```

Open Jellyfin at `http://localhost:8096`, then from repo root:

```bash
dotnet publish Jellyfin.Plugin.UploadInbox/Jellyfin.Plugin.UploadInbox.csproj -c Release
./dev/scripts/install-plugin.sh
(cd dev && docker compose restart jellyfin)
```

Configure at **Dashboard → Plugins → Upload Inbox → Settings** (e.g. base path `/inbox`).

### Testing

```bash
dotnet test Jellyfin.Plugin.UploadInbox.Tests/Jellyfin.Plugin.UploadInbox.Tests.csproj
```

See [TESTING.md](TESTING.md) for integration tests and [dev/README.md](dev/README.md) for dev environment details.

### Releasing (maintainers)

- Version is driven by Git tags: `v1.0.0.0` → release 1.0.0.0. Push a tag to trigger the release workflow.
- **GitHub Pages must be enabled** for the repository: **Settings → Pages → Source**: **GitHub Actions**.
- The release workflow:
  - builds and tests the plugin
  - publishes the ZIP and checksums to GitHub Releases
  - generates `pages/manifest.json` in Jellyfin's repository format
  - deploys that manifest to GitHub Pages
- See [RELEASE-CHECKLIST.md](RELEASE-CHECKLIST.md) for a step-by-step release and install-validation checklist.
