using Microsoft.Win32;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.WinForms;

namespace Polus.Patcher;

public sealed class MainForm : Form
{
    private const string RepoOwner = "Tvmanisback";
    private const string RepoName = "Polus";
    private const string BepInExBuildsPage = "https://builds.bepinex.dev/projects/bepinex_be";
    private static readonly HttpClient Http = CreateHttpClient();

    private readonly TextBox pathBox = new() { ReadOnly = true, Width = 430 };
    private readonly TextBox logBox = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly Button patchButton = new() { Text = "Patch", Width = 112, Height = 38 };
    private readonly Button updateButton = new() { Text = "Update Polus", Width = 112, Height = 38, Enabled = false };
    private readonly Label updateStatus = new() { Text = "Updates not checked", AutoSize = true, Padding = new Padding(4, 10, 0, 0) };
    private string? gamePath;
    private string? backupPath;
    private string? latestReleaseAssetUrl;
    private string? latestReleaseTag;
    private string? latestReleaseDigest;

    private readonly WebView2 browser = new() { Dock = DockStyle.Fill };
    private bool browserReady;

    public MainForm()
    {
        Text = "Polus";
        Width = 1040;
        Height = 760;
        MinimumSize = new Size(760, 620);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(8, 13, 18);
        Controls.Add(browser);
        Shown += async (_, _) => await InitializeBrowserAsync();
        Log("Ready. Detect your Steam installation, then run Setup loader.");
    }

    private async Task InitializeBrowserAsync()
    {
        try
        {
            await browser.EnsureCoreWebView2Async();
            browser.CoreWebView2.WebMessageReceived += async (_, args) =>
                await HandleBrowserActionAsync(args.TryGetWebMessageAsString());
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("Polus.Patcher.index.html")
                ?? throw new InvalidOperationException("The shared Polus patcher HTML resource is missing.");
            using var reader = new StreamReader(stream);
            var navigation = new TaskCompletionSource<bool>();
            browser.CoreWebView2.NavigationCompleted += (_, _) => navigation.TrySetResult(true);
            browser.NavigateToString(await reader.ReadToEndAsync());
            await navigation.Task;
            browserReady = true;
            await browser.CoreWebView2.ExecuteScriptAsync("window.Polus && window.Polus.platform('pc')");
            await UpdateBrowserAsync("path", gamePath ?? "");
            await UpdateBrowserAsync("status", "Waiting for game selection", false);
            foreach (var line in logBox.Lines.Where(line => !string.IsNullOrWhiteSpace(line)))
                await UpdateBrowserAsync("log", line);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Polus could not load its HTML interface. Install the Microsoft Edge WebView2 Runtime and restart Polus.\n\n" + ex.Message,
                "Polus interface error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task HandleBrowserActionAsync(string action)
    {
        switch (action)
        {
            case "detect": DetectSteam(); break;
            case "browse": Browse(); break;
            case "setup": await SetupAsync(); break;
            case "patch": await PatchAsync(); break;
            case "restore": Restore(); break;
            case "checkUpdates": await CheckUpdatesAsync(); break;
            case "update": await UpdatePolusAsync(); break;
        }
    }

    private async Task UpdateBrowserAsync(string method, params object[] values)
    {
        if (!browserReady || browser.CoreWebView2 == null) return;
        var args = string.Join(",", values.Select(value => JsonSerializer.Serialize(value)));
        await browser.CoreWebView2.ExecuteScriptAsync($"window.Polus && window.Polus.{method}({args})");
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(4) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Polus-Patcher/1.0");
        return client;
    }

    private void DetectSteam()
    {
        var found = FindSteamInstall();
        if (found is null) { Log("Steam install not found automatically. Use Browse."); _ = UpdateBrowserAsync("status", "Steam not detected — browse for the game folder", false); return; }
        SetGamePath(found);
        Log("Detected Steam installation: " + found);
    }

    private static string? FindSteamInstall()
    {
        var roots = new List<string>();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key?.GetValue("SteamPath") is string path) roots.Add(path.Replace('/', Path.DirectorySeparatorChar));
        }
        catch { }
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            if (key?.GetValue("InstallPath") is string path) roots.Add(path);
        }
        catch { }

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var libraries = new List<string> { root };
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(vdf), @"""path""\s*""([^""]+)"""))
                    libraries.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
            }
            foreach (var library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var candidate = Path.Combine(library, "steamapps", "common", "Among Us");
                if (File.Exists(Path.Combine(candidate, "Among Us.exe"))) return candidate;
            }
        }
        return null;
    }

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog { Description = "Select the folder containing Among Us.exe" };
        if (dialog.ShowDialog() == DialogResult.OK) SetGamePath(dialog.SelectedPath);
    }

    private void SetGamePath(string path)
    {
        if (!File.Exists(Path.Combine(path, "Among Us.exe")))
        {
            MessageBox.Show("That folder does not contain Among Us.exe.", "Invalid folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        gamePath = Path.GetFullPath(path);
        pathBox.Text = gamePath;
        _ = UpdateBrowserAsync("path", gamePath);
        _ = UpdateBrowserAsync("status", "Among Us folder selected", true);
    }

    private static bool Is64BitPe(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (reader.ReadUInt16() != 0x5A4D) return false;
        stream.Position = 0x3C;
        var peOffset = reader.ReadInt32();
        if (peOffset < 0 || peOffset > stream.Length - 6) return false;
        stream.Position = peOffset;
        return reader.ReadUInt32() == 0x00004550 && reader.ReadUInt16() == 0x8664;
    }

    private bool CloseGameIfRunning()
    {
        var processes = Process.GetProcessesByName("Among Us");
        if (processes.Length == 0) return true;
        if (MessageBox.Show("Among Us is running. Close it normally before continuing?", "Polus",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return false;
        foreach (var process in processes)
        {
            try
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(8000))
                {
                    Log("Among Us did not close. Close it manually and retry.");
                    return false;
                }
            }
            catch (Exception ex) { Log("Could not close Among Us: " + ex.Message); return false; }
        }
        return true;
    }

    private string EnsureBackupFolder()
    {
        if (backupPath is not null && Directory.Exists(backupPath)) return backupPath;
        backupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Polus", "Backups", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(backupPath);
        File.WriteAllText(Path.Combine(backupPath, "game-root.txt"), gamePath!);
        Log("Backup created: " + backupPath);
        return backupPath;
    }

    private async Task SetupAsync()
    {
        if (gamePath is null)
        {
            DetectSteam();
            if (gamePath is null) { Browse(); if (gamePath is null) return; }
        }
        if (!CloseGameIfRunning()) return;
        var exe = Path.Combine(gamePath, "Among Us.exe");
        if (!Is64BitPe(exe))
        {
            MessageBox.Show("This Polus build targets the 64-bit Steam version of Among Us. The selected executable is not x64.",
                "Unsupported game architecture", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        EnsureBackupFolder();
        try
        {
            Log("Finding the latest official BepInEx 6 IL2CPP x64 build...");
            var page = await Http.GetStringAsync(BepInExBuildsPage);
            var match = Regex.Match(page, "href=[\"']([^\"']*BepInEx-Unity\\.IL2CPP-win-x64-[^\"']+\\.zip)[\"']", RegexOptions.IgnoreCase);
            if (!match.Success) throw new InvalidDataException("Could not find the official Windows x64 IL2CPP ZIP on the BepInEx builds page.");
            var downloadUri = new Uri(new Uri(BepInExBuildsPage), System.Net.WebUtility.HtmlDecode(match.Groups[1].Value));
            if (downloadUri.Host != "builds.bepinex.dev" || !downloadUri.AbsolutePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The BepInEx download URL failed validation.");

            var tempZip = Path.Combine(Path.GetTempPath(), "Polus-BepInEx-" + Guid.NewGuid().ToString("N") + ".zip");
            var bytes = await Http.GetByteArrayAsync(downloadUri);
            if (bytes.Length < 1_000_000) throw new InvalidDataException("The downloaded BepInEx archive is unexpectedly small.");
            await File.WriteAllBytesAsync(tempZip, bytes);
            var installed = ExtractZipSafely(tempZip, gamePath, EnsureBackupFolder());
            File.Delete(tempZip);
            Log($"BepInEx installed from the official x64 IL2CPP build. Files processed: {installed}");
            Log("Setup finished. Press Patch to install the Polus plugin.");
            patchButton.Enabled = true;
        }
        catch (Exception ex)
        {
            Log("Setup failed: " + ex.Message);
            MessageBox.Show(ex.Message, "BepInEx setup failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private int ExtractZipSafely(string zipPath, string destinationRoot, string backupRoot)
    {
        var safeRoot = Path.GetFullPath(destinationRoot) + Path.DirectorySeparatorChar;
        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = 0;
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            var target = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
            if (!target.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The archive contains a path outside the game folder.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (File.Exists(target))
            {
                var relative = Path.GetRelativePath(destinationRoot, target);
                var saved = Path.Combine(backupRoot, "original", relative);
                Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
                if (!File.Exists(saved)) File.Copy(target, saved);
            }
            using var input = entry.Open();
            using var output = File.Create(target);
            input.CopyTo(output);
            changed.Add(Path.GetRelativePath(destinationRoot, target));
            count++;
        }
        File.WriteAllLines(Path.Combine(backupRoot, "changed-files.txt"), changed);
        return count;
    }

    private async Task PatchAsync()
    {
        if (gamePath is null) { MessageBox.Show("Press Setup first so Polus can detect and prepare the game folder."); return; }
        if (!CloseGameIfRunning()) return;
        try
        {
            var pluginDirectory = Path.Combine(gamePath, "BepInEx", "plugins", "Polus");
            Directory.CreateDirectory(pluginDirectory);
            var installed = 0;
            installed += ExtractEmbeddedResource("Polus.Plugin.dll", Path.Combine(pluginDirectory, "Polus.Plugin.dll"));
            installed += ExtractEmbeddedResource("Polus.RoleCore.dll", Path.Combine(pluginDirectory, "Polus.RoleCore.dll"));
            installed += ExtractEmbeddedResource("MiraAPI.dll", Path.Combine(pluginDirectory, "MiraAPI.dll"));
            installed += ExtractEmbeddedResource("Reactor.dll", Path.Combine(pluginDirectory, "Reactor.dll"));
            installed += ExtractEmbeddedResource("roles.json", Path.Combine(pluginDirectory, "roles.json"));
            if (installed != 5) throw new InvalidOperationException("The Polus plugin payload or its MiraAPI/Reactor dependencies are incomplete in this patcher build.");
            File.WriteAllText(Path.Combine(pluginDirectory, "installed-version.txt"), Application.ProductVersion);
            Log("Polus, MiraAPI, and Reactor plugin assemblies installed to BepInEx/plugins/Polus.");
            Log("Launch Among Us once to let BepInEx initialize. Check BepInEx/LogOutput.log if the plugin reports an error.");
        }
        catch (Exception ex)
        {
            Log("Patch failed: " + ex.Message);
            MessageBox.Show(ex.Message, "Polus patch failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static int ExtractEmbeddedResource(string resourceName, string targetPath)
    {
        using var input = typeof(MainForm).Assembly.GetManifestResourceStream(resourceName);
        if (input is null) return 0;
        using var output = File.Create(targetPath);
        input.CopyTo(output);
        return 1;
    }

    private void Restore()
    {
        if (gamePath is null) { MessageBox.Show("Select the game folder first."); return; }
        using var dialog = new FolderBrowserDialog { Description = "Select the Polus backup folder" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        var backup = dialog.SelectedPath;
        var list = Path.Combine(backup, "changed-files.txt");
        var rootFile = Path.Combine(backup, "game-root.txt");
        if (!File.Exists(list) || !File.Exists(rootFile) ||
            !string.Equals(Path.GetFullPath(File.ReadAllText(rootFile).Trim()), Path.GetFullPath(gamePath), StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("This backup is invalid or belongs to a different game folder.");
            return;
        }
        if (!CloseGameIfRunning()) return;
        var safeRoot = Path.GetFullPath(gamePath) + Path.DirectorySeparatorChar;
        foreach (var relative in File.ReadAllLines(list))
        {
            var target = Path.GetFullPath(Path.Combine(gamePath, relative));
            if (!target.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase)) continue;
            var saved = Path.Combine(backup, "original", relative);
            if (File.Exists(saved))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(saved, target, true);
            }
            else if (File.Exists(target)) File.Delete(target);
        }
        var polusPlugin = Path.Combine(gamePath, "BepInEx", "plugins", "Polus");
        if (Directory.Exists(polusPlugin)) Directory.Delete(polusPlugin, true);
        Log("Restore completed for backed-up loader files and removed the Polus plugin folder.");
    }

    private async Task CheckUpdatesAsync()
    {
        updateButton.Enabled = false;
        updateStatus.Text = "Checking...";
        try
        {
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            using var response = await Http.GetAsync(url);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new InvalidOperationException("No GitHub release exists yet. Publish a vX.Y.Z release to enable the updater.");
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            latestReleaseTag = root.GetProperty("tag_name").GetString();
            var assets = root.GetProperty("assets").EnumerateArray();
            var asset = assets.FirstOrDefault(a => string.Equals(a.GetProperty("name").GetString(), "Polus.exe", StringComparison.OrdinalIgnoreCase));
            if (asset.ValueKind == JsonValueKind.Undefined)
                throw new InvalidDataException("The latest GitHub release does not contain the required Polus.exe asset.");
            latestReleaseAssetUrl = asset.GetProperty("browser_download_url").GetString();
            latestReleaseDigest = asset.TryGetProperty("digest", out var digest) ? digest.GetString() : null;
            var current = typeof(MainForm).Assembly.GetName().Version ?? new Version(0, 0);
            var latestText = (latestReleaseTag ?? "").TrimStart('v', 'V');
            var isNewer = Version.TryParse(latestText, out var latest) && latest > current;
            updateStatus.Text = isNewer ? $"Update available: {latestReleaseTag}" : $"Latest: {latestReleaseTag}";
            _ = UpdateBrowserAsync("updates", updateStatus.Text, isNewer);
            updateButton.Enabled = isNewer;
            Log(isNewer ? $"Polus update available: {latestReleaseTag}" : $"Polus is current ({latestReleaseTag}).");
        }
        catch (Exception ex)
        {
            updateStatus.Text = "Update check failed";
            _ = UpdateBrowserAsync("updates", updateStatus.Text, false);
            Log("Update check failed: " + ex.Message);
            MessageBox.Show(ex.Message, "Polus updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async Task UpdatePolusAsync()
    {
        if (string.IsNullOrWhiteSpace(latestReleaseAssetUrl)) return;
        if (!Uri.TryCreate(latestReleaseAssetUrl, UriKind.Absolute, out var uri) || uri.Host != "github.com")
        {
            MessageBox.Show("The release asset URL did not pass validation.");
            return;
        }
        try
        {
            updateButton.Enabled = false;
            Log("Downloading Polus.exe from the latest GitHub release...");
            var bytes = await Http.GetByteArrayAsync(uri);
            if (bytes.Length < 100_000 || bytes[0] != (byte)'M' || bytes[1] != (byte)'Z')
                throw new InvalidDataException("The release asset is not a valid Windows executable.");
            if (!string.IsNullOrWhiteSpace(latestReleaseDigest) && latestReleaseDigest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (!string.Equals(actual, latestReleaseDigest[7..], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The release asset SHA-256 digest does not match GitHub's digest.");
            }

            var currentExe = Application.ExecutablePath;
            var updateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Polus", "Updates");
            Directory.CreateDirectory(updateDir);
            var newExe = Path.Combine(updateDir, "Polus.new.exe");
            var script = Path.Combine(updateDir, "apply-update.cmd");
            await File.WriteAllBytesAsync(newExe, bytes);
            var pid = Environment.ProcessId;
            var escapedNew = newExe.Replace("%", "%%");
            var escapedCurrent = currentExe.Replace("%", "%%");
            var scriptText = $"@echo off\r\n:wait\r\ntasklist /FI \"PID eq {pid}\" | find \"{pid}\" >nul\r\nif not errorlevel 1 (timeout /t 1 /nobreak >nul & goto wait)\r\ncopy /y \"{escapedNew}\" \"{escapedCurrent}\" >nul\r\nif errorlevel 1 exit /b 1\r\nstart \"\" \"{escapedCurrent}\"\r\ndel \"{escapedNew}\"\r\ndel \"%~f0\"\r\n";
            await File.WriteAllTextAsync(script, scriptText);
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"") { CreateNoWindow = true, UseShellExecute = false });
            Log($"Installing {latestReleaseTag}. Polus will restart after this window closes.");
            Close();
        }
        catch (Exception ex)
        {
            updateButton.Enabled = true;
            Log("Update failed: " + ex.Message);
            MessageBox.Show(ex.Message, "Polus update failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        logBox.AppendText(line + Environment.NewLine);
        _ = UpdateBrowserAsync("log", line);
        _ = UpdateBrowserAsync("status", message, message.Contains("finished", StringComparison.OrdinalIgnoreCase)
            || message.Contains("installed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("completed", StringComparison.OrdinalIgnoreCase));
    }
}
