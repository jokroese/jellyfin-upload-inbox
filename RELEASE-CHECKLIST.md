# Release checklist (maintainers)

Use this when cutting a new release and validating the distribution path.

## Before tagging

- [ ] `build.yaml`: set `version` and `changelog` for the release (optional; workflow stamps version from the tag).
- [ ] **GitHub Pages** is enabled: **Settings → Pages → Source: GitHub Actions**. Required for the repository URL to work.

## Release

- [ ] Create and push tag, e.g. `git tag v1.0.0.0 && git push origin v1.0.0.0`.
- [ ] Wait for the [Release](https://github.com/jokroese/jellyfin-upload-inbox/actions) workflow to complete.
- [ ] Confirm **GitHub Release** exists with assets: `UploadInbox_<version>.zip`, `.zip.sha256`, `.zip.md5`.
- [ ] Confirm https://jokroese.github.io/jellyfin-upload-inbox/manifest.json returns JSON.

## Install validation (first install test completed on Jellyfin 10.11.x)

On a **clean** Jellyfin 10.11.x instance:

- [ ] **Dashboard → Plugins → Repositories** → add `https://jokroese.github.io/jellyfin-upload-inbox/manifest.json` → Save.
- [ ] **Catalog** → find **Upload Inbox** → Install.
- [ ] Restart Jellyfin.
- [ ] **Dashboard → Plugins** → confirm Upload Inbox is listed.
- [ ] Main menu → confirm **Upload Inbox** page appears.
- [ ] Configure one target (e.g. base path `/inbox`), save.
- [ ] Upload a small file; confirm it appears in the configured directory.

Optional: keep a screenshot of the repository entry and/or Catalog entry for the README or release notes.
