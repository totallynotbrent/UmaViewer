# Auto-update

UmaViewer itself has no in-app update UI. The viewer launches and runs; updating is
manual: download the latest `UmaViewer-Windows-x64.zip` from the release page and
replace the install directory.

## The release pipeline

Each release ships `UmaViewer-Windows-x64.zip`, plus a `.sha256` sidecar for installs
(or people) that want to verify the download. The zip contains only the player and
its data, no auxiliary executables.

## Why there is no updater executable

An earlier experiment bundled a standalone updater and a release-checking launcher
into the zip. It doubled the download size (two self-contained .NET executables
worth ~140 MB compressed) and introduced a second launch target, so it was removed
in favor of the direct-download flow above.