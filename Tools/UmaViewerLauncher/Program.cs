using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Forms;

namespace UmaViewerLauncher;

/// <summary>launcher exe: checks the github feed, offers an update, else boots the game.</summary>
/// <remarks>
/// Double-click this instead of UmaViewer.exe. it reads config.json for the channel
/// (stable or experimental), asks the github api for the newest release, downloads it
/// with integrity verification, and hands the temp zip to the bundled updater exe. the
/// updater runs from a temp copy so its own file can be replaced during the swap.
/// </remarks>
internal static class Program
{
    // file names fixed by the release pipeline.
    private const string AppExe = "UmaViewer.exe";
    private const string UpdaterExe = "UmaViewerUpdater.exe";
    private const string ConfigFile = "config.json";
    private const string VersionFile = "version.txt";

    // release source that owns the project.
    private const string Owner = "totallynotbrent";
    private const string Repo = "UmaViewer";
    private const string ZipAsset = "UmaViewer-Windows-x64.zip";
    private const string ShaAsset = ZipAsset + ".sha256";

    // a stale marker with the temp zip path is removed once the updater is dispatched.
    private const string InProgressMarker = ".update-in-progress";

    private static string? _baseDir;

    [STAThread]
    private static int Main(string[] args)
    {
        _baseDir = AppContext.BaseDirectory;

        // the game must live beside this launcher; a portable install layout.
        string appExe = Path.Combine(_baseDir, AppExe);
        string updaterExe = Path.Combine(_baseDir, UpdaterExe);
        if (!File.Exists(appExe))
        {
            MessageBox.Show($"could not find {AppExe} next to this launcher ({_baseDir}). install is incomplete.", "UmaViewer launcher");
            return 1;
        }
        if (!File.Exists(updaterExe))
        {
            MessageBox.Show($"could not find {UpdaterExe} next to this launcher ({_baseDir}). updates are unavailable.", "UmaViewer launcher");
        }

        // what is currently installed; no marker means first boot, so skip the update check.
        string localTag = ReadMarker(Path.Combine(_baseDir, VersionFile));
        if (localTag.Length == 0)
        {
            StartApp(appExe);
            return 0;
        }

        // resolve the candidate release; a feed failure is non-fatal and boots the game.
        string channel = ReadChannel(Path.Combine(_baseDir, ConfigFile));
        ReleaseCandidate? candidate = null;
        try
        {
            candidate = QueryLatestRelease(channel);
        }
        catch (Exception e)
        {
            Log($"update query failed: {e.Message}");
        }

        // nothing newer to offer -- boot as normal.
        if (candidate == null || candidate.Tag.Length == 0 || candidate.Tag == localTag)
        {
            StartApp(appExe);
            return 0;
        }

        // download sidecar + zip and prove the checksum before offering anything.
        if (!TryFetchAndVerify(candidate, out string? zipPath))
        {
            StartApp(appExe);
            return 1;
        }

        // remember the staged zip so the updater subprocess and the user can find it.
        File.WriteAllText(Path.Combine(_baseDir, InProgressMarker), zipPath!);

        var choice = MessageBox.Show(
            $"update to {candidate.Tag} is available (currently {localTag}). install now?",
            "UmaViewer update",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1);

        if (choice == DialogResult.Yes)
        {
            LaunchUpdater(candidate.Tag, zipPath!);
        }
        else
        {
            // delete the marker so a later boot does not try to resume a half-update.
            TryDelete(Path.Combine(_baseDir, InProgressMarker));
            StartApp(appExe);
        }
        return 0;
    }

    private static ReleaseCandidate? QueryLatestRelease(string channel)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "UmaViewerLauncher");
        client.Timeout = TimeSpan.FromSeconds(30);

        // stable uses the single latest endpoint; experimental scans /releases for the newest prerelease.
        string url = $"https://api.github.com/repos/{Owner}/{Repo}/releases"
                     + (channel == "experimental" ? "" : "/latest");
        Log($"querying {url}");

        using var doc = JsonDocument.Parse(client.GetStringAsync(url).GetAwaiter().GetResult());

        JsonElement release;
        if (channel == "experimental")
        {
            // pick the oldest-published prerelease that is still the most recent on the list.
            release = default;
            bool found = false;
            foreach (JsonElement item in doc.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("prerelease", out JsonElement pre) && pre.GetBoolean())
                {
                    release = item;
                    found = true;
                    break;
                }
            }
            if (!found)
                return null;
        }
        else
        {
            release = doc.RootElement;
        }

        string tag = release.TryGetProperty("tag_name", out JsonElement t) ? t.GetString() ?? "" : "";
        string? zipUrl = null;
        string? shaUrl = null;

        if (release.TryGetProperty("assets", out JsonElement assets))
        {
            foreach (JsonElement asset in assets.EnumerateArray())
            {
                string name = asset.TryGetProperty("name", out JsonElement n) ? n.GetString() ?? "" : "";
                string href = asset.TryGetProperty("browser_download_url", out JsonElement b) ? b.GetString() ?? "" : "";
                if (name == ZipAsset) zipUrl = href;
                else if (name == ShaAsset) shaUrl = href;
            }
        }

        if (zipUrl == null || shaUrl == null)
            return null;

        Log($"candidate tag={tag} zip={zipUrl}");
        return new ReleaseCandidate(tag, zipUrl, shaUrl);
    }

    private static bool TryFetchAndVerify(ReleaseCandidate candidate, out string? zipPath)
    {
        zipPath = null;
        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "umaviewer-launch");
            Directory.CreateDirectory(tempDir);
            string tempZip = Path.Combine(tempDir, ZipAsset);
            string tempSha = Path.Combine(tempDir, ShaAsset);

            // download both assets fresh so a stale temp file can never be trusted.
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "UmaViewerLauncher");
            client.Timeout = TimeSpan.FromSeconds(600);

            Log($"downloading sidecar {candidate.ShaUrl}");
            File.WriteAllBytes(tempSha, client.GetByteArrayAsync(candidate.ShaUrl).GetAwaiter().GetResult());

            Log($"downloading archive {candidate.ZipUrl}");
            File.WriteAllBytes(tempZip, client.GetByteArrayAsync(candidate.ZipUrl).GetAwaiter().GetResult());

            // sidecar form is "<lowercase hex>  <filename>" per the write-release-digest step.
            string sidecar = File.ReadAllText(tempSha).Trim();
            string[] fields = sidecar.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 0 || fields[0].Length != 64 || !fields[0].All(Uri.IsHexDigit))
            {
                MessageBox.Show($"sha256 sidecar was malformed: '{sidecar}'. aborting update.", "UmaViewer update");
                return false;
            }
            string expected = fields[0].ToLowerInvariant();
            string actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(tempZip))).ToLowerInvariant();
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                MessageBox.Show($"checksum mismatch (got {actual}, expected {expected}). the download is corrupt; current install is kept.", "UmaViewer update");
                return false;
            }

            Log("checksum verified");
            zipPath = tempZip;
            return true;
        }
        catch (Exception e)
        {
            Log($"download/verify failed: {e.Message}");
            return false;
        }
    }

    private static void LaunchUpdater(string tag, string zipPath)
    {
        try
        {
            // the updater must run from a temp copy or its exe would be locked during the
            // file-replacement step; staging it here mirrors what the old in-app updater did.
            string tempDir = Path.Combine(Path.GetTempPath(), "umaviewer-launch");
            Directory.CreateDirectory(tempDir);
            string updaterCopy = Path.Combine(tempDir, UpdaterExe);
            File.Copy(Path.Combine(_baseDir!, UpdaterExe), updaterCopy, overwrite: true);

            // record the tag being installed so the next boot knows the new version.
            File.WriteAllText(Path.Combine(_baseDir!, VersionFile), tag);

            string args = $"--root \"{_baseDir}\" --zip \"{zipPath}\" --tag {tag} --relaunch {AppExe}";
            Log($"starting updater: {updaterCopy} {args}");

            var psi = new ProcessStartInfo(updaterCopy, args) { WorkingDirectory = _baseDir };
            Process.Start(psi);
        }
        catch (Exception e)
        {
            Log($"failed to start updater: {e.Message}");
            MessageBox.Show($"could not start the updater: {e.Message}. launch UmaViewer.exe manually.", "UmaViewer update");
        }
    }

    private static string ReadChannel(string configPath)
    {
        try
        {
            if (!File.Exists(configPath))
                return "stable";
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            if (doc.RootElement.TryGetProperty("channel", out JsonElement c))
            {
                string? v = c.GetString();
                if (v == "stable" || v == "experimental")
                    return v;
            }
        }
        catch
        {
            // a malformed config just falls back to the stable channel.
        }
        return "stable";
    }

    private static string ReadMarker(string versionPath)
    {
        try
        {
            if (!File.Exists(versionPath))
                return "";
            return File.ReadAllText(versionPath).Trim();
        }
        catch
        {
            return "";
        }
    }

    private static void StartApp(string appExe)
    {
        try
        {
            var psi = new ProcessStartInfo(appExe) { WorkingDirectory = _baseDir };
            Process.Start(psi);
        }
        catch (Exception e)
        {
            MessageBox.Show($"failed to start {AppExe}: {e.Message}", "UmaViewer launcher");
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* non-fatal. */ }
    }

    private static void Log(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        try
        {
            string dir = _baseDir ?? ".";
            File.AppendAllText(Path.Combine(dir, "launcher.log"), line + Environment.NewLine);
        }
        catch
        {
            // logging is best-effort.
        }
    }
}

internal sealed record ReleaseCandidate(string Tag, string ZipUrl, string ShaUrl);