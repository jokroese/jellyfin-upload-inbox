# Upload Inbox – local Jellyfin dev environment

This folder provides a fast local test loop for the plugin using Docker Compose.

## Why this exists

Jellyfin-in-Docker persists configuration (and plugins) under `/config`.  [oai_citation:4‡Jellyfin](https://jellyfin.org/docs/general/installation/container/?utm_source=chatgpt.com)
Manual plugin installs for Docker go under `/config/plugins/<PluginName>/...` and require a restart.  [oai_citation:5‡Demon Warrior Tech Docs](https://docs.demonwarriortech.com/Jellyfin%20Extras/Jellyfin-Plugins/?utm_source=chatgpt.com)

This dev compose mirrors that so we can test changes without touching the deployed instance.

---

## 1) One-time setup

From repo root:

```bash
cp dev/.env.example dev/.env
mkdir -p dev/jf-config dev/jf-cache dev/inbox
```

Bring up local Jellyfin:

```bash
cd dev
docker compose --env-file .env up -d
```

Open: `http://localhost:8096` (or whatever you set in `dev/.env`).

Complete Jellyfin’s initial setup wizard (admin user etc.).

---

## 2) Build the plugin

From repo root:

```bash
dotnet clean
dotnet publish Jellyfin.Plugin.UploadInbox/Jellyfin.Plugin.UploadInbox.csproj -c Release
```

Publish output will be in:

`Jellyfin.Plugin.UploadInbox/bin/Release/net9.0/publish/`

---

## 3) Install the plugin into local Jellyfin

Manual install path in Docker is under the persisted `/config` mount:

`dev/jf-config/plugins/UploadInbox/`

Copy the published output:

```bash
mkdir -p dev/jf-config/plugins/UploadInbox
cp -R Jellyfin.Plugin.UploadInbox/bin/Release/net9.0/publish/* dev/jf-config/plugins/UploadInbox/
```

Restart Jellyfin to load it:

```bash
docker compose -f dev/docker-compose.yml restart jellyfin
```

Jellyfin should now show the plugin under:
Dashboard → Plugins.  [oai_citation:6‡Demon Warrior Tech Docs](https://docs.demonwarriortech.com/Jellyfin%20Extras/Jellyfin-Plugins/?utm_source=chatgpt.com)

---

## 4) Configure Upload Inbox

1. Dashboard → Plugins → Upload Inbox (configuration page)
2. Add one target:
   - **Display name**: e.g. `Inbox`
   - **Base path (on server)**: `/inbox` (this is mounted from `dev/inbox`)
   - **Allowed user IDs**: add your current user Guid (see below)

### Find your user Guid

In Jellyfin: Dashboard → Users → click your user → the URL contains the user id.

---

## 5) Test an upload

1. Open the Upload Inbox page in Jellyfin (the plugin page).
2. Select the target.
3. Upload a small file (~1–10 MB).
4. Confirm it appears on the host in `dev/inbox/` (or inside the container at `/inbox`).

---

## 6) If uploads fail behind a reverse proxy

Reverse proxies often reject large request bodies (413) unless configured.
For Nginx, see Jellyfin’s reverse proxy docs.  [oai_citation:7‡Jellyfin](https://jellyfin.org/docs/general/post-install/networking/reverse-proxy/nginx/?utm_source=chatgpt.com)

---

## 7) Tear down

```bash
cd dev
docker compose down
```

This does NOT delete `dev/jf-config` unless you remove it yourself.
