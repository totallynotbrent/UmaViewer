# Auto-update

UmaViewer itself has no in-app update UI. Updating is manual: either download
the latest `UmaViewer-Windows-x64.zip` from the release page, or run the
`update.bat` shipped in the zip to fetch a chosen channel (stable or
experimental) straight from GitHub and unpack it over the install folder.

## The release pipeline

Each release ships `UmaViewer-Windows-x64.zip`, `update.bat`, and a `.sha256`
sidecar for installs (or people) that want to verify the download. The zip
contains the player, its data, and the updater script, no auxiliary
executables.

## Why there is no updater executable

An earlier experiment bundled a standalone updater and a release-checking launcher
into the zip. It doubled the download size (two self-contained .NET executables
worth ~140 MB compressed) and introduced a second launch target, so it was removed
in favor of the script-based flow above.