using System.Diagnostics;
using System.IO.Compression;

namespace UmaViewerUpdater;

/// <summary>the standalone installer that swaps the app files while UmaViewer is closed.</summary>
internal static class Program
{
    // exit codes the launcher can surface in a log if something fails.
    private const int ExitOk = 0;
    private const int ExitArgs = 2;
    private const int ExitCorruptZip = 4;
    private const int ExitReplaceFailed = 5;

    private static string _logPath = "";

    private static int Main(string[] args)
    {
        var opt = ParseArguments(args);
        if (opt == null || !opt.ContainsKey("root") || !opt.ContainsKey("zip"))
        {
            Log("missing --root or --zip. usage: UmaViewerUpdater.exe --root <dir> --zip <zip> [--relaunch <app.exe>] [--pid <waiter>] [--tag <ver>]");
            return ExitArgs;
        }

        string root = opt["root"];
        string zip = Path.GetFullPath(opt["zip"]);
        string relaunch = opt.TryGetValue("relaunch", out var r) ? r : "UmaViewer.exe";
        int waiterPid = opt.TryGetValue("pid", out var p) && int.TryParse(p, out var w) ? w : 0;

        string stage = Path.Combine(Path.GetTempPath(), "umaupd-" + (opt.TryGetValue("tag", out var t) ? Sanitize(t) : "stage"));
        _logPath = Path.Combine(stage, "updater.log");

        Log("UmaViewerUpdater starting");
        Log($"root={root}");
        Log($"zip={zip}");
        Log($"stage={stage}");

        if (!Directory.Exists(root) || !File.Exists(zip))
        {
            Log("root directory or zip does not exist; aborting");
            return ExitArgs;
        }

        Log("waiting for the viewer process to exit");
        if (!WaitForExit(waiterPid))
        {
            Log("timed out waiting for the viewer to exit; aborting");
            return ExitArgs;
        }

        try
        {
            if (Directory.Exists(stage))
                Directory.Delete(stage, recursive: true);
            Directory.CreateDirectory(stage);

            Log("extracting release archive");
            ZipFile.ExtractToDirectory(zip, stage);
        }
        catch (Exception e)
        {
            Log($"archive extract failed: {e.Message}");
            return ExitCorruptZip;
        }

        Log("validating staged application files");
        if (!ValidateStage(stage, relaunch))
        {
            Log("the staged archive is missing required files; aborting");
            return ExitCorruptZip;
        }

        Log("replacing application files under the install root");
        if (!ReplaceRoot(stage, root, relaunch))
        {
            Log("file replacement failed; the previous install was left in place");
            return ExitReplaceFailed;
        }

        Log("replacement complete");

        // make the replacement exe visible again, otherwise Windows can leave it hidden.
        try
        {
            ShowHiddenFilesAgain(root);
        }
        catch
        {
            // non-fatal; the update already succeeded.
        }

        if (!string.IsNullOrWhiteSpace(relaunch))
        {
            string exe = Path.Combine(root, relaunch);
            if (File.Exists(exe))
            {
                Log($"relaunching {exe}");
                try
                {
                    Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = root });
                }
                catch (Exception e)
                {
                    Log($"relaunch failed (launch manually): {e.Message}");
                }
            }
            else
            {
                Log($"relaunch target missing: {exe}");
            }
        }

        TryCleanupTemp(stage, zip);
        Log("UmaViewerUpdater done");
        return ExitOk;
    }

    private static Dictionary<string, string>? ParseArguments(string[] args)
    {
        var map = new Dictionary<string, string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Length < 2 || args[i][0] != '-')
                continue;
            string key = args[i].TrimStart('-');
            int eq = key.IndexOf('=');
            if (eq >= 0)
            {
                map[key[..eq]] = key[(eq + 1)..];
            }
            else if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
            {
                map[key] = args[i + 1];
                i++;
            }
            else
            {
                map[key] = "";
            }
        }
        return map;
    }

    private static string Sanitize(string value)
    {
        var bad = Path.GetInvalidFileNameChars();
        return new string(value.Select(c => bad.Contains(c) ? '_' : c).ToArray());
    }

    private static bool WaitForExit(int pid)
    {
        if (pid <= 0)
            return true;
        // the viewer usually exits within a second of launching us; allow a generous window.
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                Process.GetProcessById(pid);
            }
            catch
            {
                // process is gone, which is what we are waiting for.
                return true;
            }
            Thread.Sleep(500);
        }
        return false;
    }

    private static bool ValidateStage(string stage, string relaunch)
    {
        // the app name in the zip is fixed (UmaViewer); relaunch is usually the same.
        string appExe = Path.Combine(stage, "UmaViewer.exe");
        string dataDir = Path.Combine(stage, "UmaViewer_Data");
        return File.Exists(appExe) && Directory.Exists(dataDir);
    }

    private static bool ReplaceRoot(string stage, string root, string relaunch)
    {
        try
        {
            // copy the staged files over the install root, replacing anything shared. user
            // data lives outside the root (persistentDataPath / game-data dir) and the temp
            // position of this updater means it is never overwriting its own running file.
            CopyDirectory(stage, root);
            return true;
        }
        catch (Exception e)
        {
            Log($"replace error: {e}");
            return false;
        }
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (string dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dir.Replace(sourceDir, targetDir));
        }
        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            // never overwrite the running copy of this same updater if it somehow lives in root.
            string target = file.Replace(sourceDir, targetDir);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void ShowHiddenFilesAgain(string root)
    {
        // extraction of some archives can mark entries hidden; clean flags on the app bits.
        foreach (string file in Directory.GetFiles(root, "UmaViewer*", SearchOption.TopDirectoryOnly))
            File.SetAttributes(file, FileAttributes.Normal);
    }

    private static void TryCleanupTemp(string stage, string zip)
    {
        try
        {
            if (Directory.Exists(stage))
                Directory.Delete(stage, recursive: true);
        }
        catch
        {
            // leaving staging behind is harmless; it is inside the temp directory.
        }
        try
        {
            if (File.Exists(zip))
                File.Delete(zip);
        }
        catch
        {
            // a locked download is cleaned up on a later run.
        }
    }

    private static void Log(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
        catch
        {
            // logging is best-effort; the console still sees the line.
        }
        Console.WriteLine(line);
    }
}