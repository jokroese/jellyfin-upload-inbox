# Local Jellyfin dev environment

This folder provides a local Jellyfin instance (Docker Compose) so you can iterate on the plugin without touching a real server.

## What this does

Jellyfin stores configuration (including manually-installed plugins) under `/config` in the container.
This Compose setup mounts `./jf-config` as `/config` so plugin installs persist across restarts.

---

## 1) One-time setup

From repo root:

```bash
cp dev/.env.example dev/.env
mkdir -p dev/jf-config dev/jf-cache dev/media
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

Use the helper script (recommended):

```bash
./dev/scripts/install-plugin.sh
```

Restart Jellyfin to load it:

```bash
docker compose -f dev/docker-compose.yml restart jellyfin
```

Jellyfin should now show the plugin under: Dashboard → Plugins.

---

## 4) Configure Upload Inbox

1. In Jellyfin, create a media library rooted at `/media`
2. Dashboard → Plugins → Upload Inbox
3. Add one target and select the `/media` library root
4. Save

Uploads now go directly into a configured Jellyfin library root rather than an arbitrary server folder.

### Notes

- In Docker, `/media` is the path visible inside the container.
- On the host, files will appear in `dev/media/`.

---

## 5) Test an upload

1. Open the Upload Inbox page in Jellyfin (the plugin page).
2. Select the target.
3. Upload a small file (~1–10 MB).
4. Confirm it appears on the host in `dev/media/` (or inside the container at `/media`).

---

## 6) Troubleshooting

### Plugin changes don’t appear

- Ensure you ran `dotnet publish ... -c Release`
- Run `./dev/scripts/install-plugin.sh`
- Restart Jellyfin: `cd dev && docker compose restart jellyfin`

### Uploads fail with 413

Reverse proxies often reject large request bodies unless configured to allow them.
If you are testing behind a reverse proxy, increase its request body limit.

---

## 7) Tear down

```bash
cd dev
docker compose down
```

This does NOT delete `dev/jf-config` unless you remove it yourself.
