# Auto-update

UmaViewer itself has no in-app update UI. The viewer launches and runs; updating is
handled outside the app by the bundled updater executable and the release pipeline.

## The release pipeline

Each release ships `UmaViewer-Windows-x64.zip` and a matching `.sha256` sidecar. The build also
bundles `UmaViewerUpdater.exe` and `UmaViewerLauncher.exe` into the release so the launcher can
activate the updater directly.

## The launcher

`UmaViewerLauncher.exe` is the double-click target that sits beside `UmaViewer.exe`. On startup it
reads `config.json` (same folder) for the update channel — `"channel": "stable"` (default) or
`"experimental"` — then asks the GitHub API for the newest matching release. A `version.txt` marker
in the install folder records the tag that was installed; a newer remote tag triggers a
Yes/No `MessageBox` offering the update. On "No", or when nothing newer is found, the game launches
normally. If no `version.txt` is present (fresh install) or the feed cannot be reached, the game
simply starts without a dialog. The launcher never touches the game-data directory and needs no
admin rights.

## The updater process

`UmaViewerUpdater.exe` swaps a release over an existing install. It is meant to run from a
temporary copy (the launcher copies it to `%TEMP%` first, so it is never locked by the exe it
replaces). Given `--root <dir> --zip <release.zip>` it waits for a running viewer to exit, extracts
the release to a staging directory, verifies the expected application files are present, replaces
the binaries, then optionally relaunches `UmaViewer.exe` via `--relaunch`. A missing or corrupt
archive, an unexpected release layout, or a read-only install directory abort the update and leave
the previous install untouched. The zip and staging are cleaned up after a successful install.

## What survives an update

Updates only touch the installation directory. User settings and saved paths live under the
Unity `persistentDataPath` (e.g. `%USERPROFILE%\AppData\LocalLow\...`) and the game-data
directory you point UmaViewer at, both of which are outside the install root and are left
untouched. Anything placed in the install folder that is not part of a release is kept.

## Integrity

The `.sha256` sidecar lets a caller verify the downloaded zip before installing. Updates never
downgrade: versioning is semantic and driven by release tags.

## Requirements and limitations

- Windows x64 only (matches the current IL2CPP build).
- Portable installs are supported: extracting a release over an existing installation still
  works; the updater does not assume `Program Files`, any writable folder works.
- A failed update never removes the previous working install. If the install directory becomes
  read-only, another UmaViewer is still running, the download is interrupted, or a process is
  holding a file, the update aborts and the app keeps running unchanged.