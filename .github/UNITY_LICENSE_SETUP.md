# Unity License Setup for CI (Windows x64 IL2CPP Release)

The GitHub Actions workflow (`.github/workflows/build.yml`) builds a Windows x64 IL2CPP
player and (optionally) publishes a GitHub Release. To run, the repository needs Unity
secrets so `game-ci/unity-builder` can activate a license on the ephemeral runner.

> IL2CPP builds **require an activated Unity license** — there is no free/offline path for
> IL2CPP. The `UNITY_LICENSE` `.ulf` file is **machine-bound** (it binds to the runner's
> `/etc/machine-id`), so a Personal `.ulf` cannot be reused on GitHub runners. The reliable
> route is **serial/account activation**.

## Required secrets

Set these in the repo:
`Settings → Secrets and variables → Actions → New repository secret`

| Secret | Required | Notes |
|--------|----------|-------|
| `UNITY_LICENSE` | *or* | The whole `Unity_lic.ulf` file contents (base64 or raw XML). Works only if it isn't machine-bound. |
| `UNITY_SERIAL` | *or* | A Unity serial/key, e.g. `U6-XXXX-XXXX-XXXX-XXXX`. |
| `UNITY_EMAIL` | if serial | Unity account email. MUST match the local Unity license. |
| `UNITY_PASSWORD` | if serial | Unity account password. MUST match `UNITY_EMAIL`. |

Preference order (best → worst):
1. **`UNITY_SERIAL` + `UNITY_EMAIL` + `UNITY_PASSWORD`** — `unity-builder` activates the
   serial on the runner. Reliable for CI. Use a licensed serial (Unity Personal via a
   key, or Plus/Pro). For a Personal serial, the workflow's `solutionProjectName` /
   `components` may need `set as windows build support` — the build target is already
   `StandaloneWindows64`.

Need to create a `.ulf` for the runner? Generate a manual license file in Unity:
`Unity → Edit → Licensing → Manual Activation` (login Unity ID), upload the `.alf`
and place the returned `.ulf` contents into `UNITY_LICENSE`.

## To set secrets from CLI (as repo owner)

```bash
gh secret set UNITY_EMAIL      --repo totallynotbrent/UmaViewer
gh secret set UNITY_PASSWORD   --repo totallynotbrent/UmaViewer
gh secret set UNITY_SERIAL     --repo totallynotbrent/UmaViewer
```

## How to trigger a release

- **Push a tag** `vX.Y.Z` → builds + publishes a Release.
- **Run the workflow manually** → enter a `release_tag` (e.g. `v1.0.0`) to build + release,
  or leave empty to build only (artifact upload).

The release asset is `UmaViewer-Windows-x64.zip` (IL2CPP, 64-bit).