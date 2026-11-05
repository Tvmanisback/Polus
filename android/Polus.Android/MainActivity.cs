using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using MelonLoader.Installer.Core;

namespace Polus.Android;

[Activity(Label = "Polus", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity, IPatchLogger
{
    private const string GamePackage = "com.innersloth.spacemafia";
    private TextView _status = null!;
    private Button _patch = null!;
    private readonly HttpClient _http = new();

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        BuildUi();
        RefreshGameStatus();
    }

    private void BuildUi()
    {
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(36, 48, 36, 24);
        var title = new TextView(this) { Text = "POLUS", TextSize = 32, Gravity = Android.Views.GravityFlags.Center };
        root.AddView(title);
        root.AddView(new TextView(this) { Text = "LemonLoader Android patcher", TextSize = 18, Gravity = Android.Views.GravityFlags.Center });
        _status = new TextView(this) { TextSize = 14 };
        root.AddView(_status);
        _patch = new Button(this) { Text = "Patch installed Among Us" };
        _patch.Click += async (_, _) => await PatchInstalledGame();
        root.AddView(_patch);
        var note = new TextView(this) { Text = "Polus uses the LemonLoader patch engine directly. Patching creates newly signed APK(s); Android normally cannot install them as an update over the Play Store-signed game. Back up your game data first. This tool does not delete the original installation or silently install the patched build." };
        root.AddView(note);
        SetContentView(root);
    }

    private void RefreshGameStatus()
    {
        try
        {
            var info = PackageManager!.GetApplicationInfo(GamePackage, 0);
            var paths = GetApkPaths(info);
            _status.Text = $"Found Among Us. APK parts: {paths.Length}. Device ABI: {string.Join(", ", Build.SupportedAbis ?? Array.Empty<string>())}";
            _patch.Enabled = Build.SupportedAbis?.Contains("arm64-v8a") == true;
        }
        catch (PackageManager.NameNotFoundException)
        {
            _status.Text = "Among Us is not installed. Install the official game first.";
            _patch.Enabled = false;
        }
    }

    private string[] GetApkPaths(ApplicationInfo info)
    {
        var paths = new List<string> { info.SourceDir! };
        if (info.SplitSourceDirs != null) paths.AddRange(info.SplitSourceDirs);
        return paths.Where(File.Exists).Distinct().ToArray();
    }

    private async Task PatchInstalledGame()
    {
        _patch.Enabled = false;
        try
        {
            var app = PackageManager!.GetApplicationInfo(GamePackage, 0);
            var paths = GetApkPaths(app);
            if (paths.Length == 0) throw new InvalidOperationException("No installed APK files were readable.");
            var armSplit = paths.FirstOrDefault(p => Path.GetFileName(p).Contains("arm64", StringComparison.OrdinalIgnoreCase));
            var baseApk = paths.First(p => p == app.SourceDir);
            var split = armSplit != null;
            if (split && armSplit == baseApk) throw new InvalidOperationException("Could not identify the ARM64 split APK required by LemonLoader.");

            var work = Path.Combine(CacheDir!.AbsolutePath, "polus-lemonloader");
            var input = Path.Combine(work, "input");
            var output = Path.Combine(work, "output");
            Directory.CreateDirectory(input);
            Directory.CreateDirectory(output);
            var copiedBase = await CopyToPrivateStorage(baseApk, input);
            string copiedLib = "";
            string[] extras = Array.Empty<string>();
            if (split)
            {
                copiedLib = await CopyToPrivateStorage(armSplit!, input);
                extras = paths.Where(p => p != baseApk && p != armSplit).Select(p => CopyToPrivateStorage(p, input).GetAwaiter().GetResult()).ToArray();
            }

            SetStatus("Downloading LemonLoader runtime assets…");
            var dataPath = Path.Combine(work, "melon_data.zip");
            using (var response = await _http.GetAsync("https://api.github.com/repos/LemonLoader/MelonLoader/releases/latest"))
            {
                response.EnsureSuccessStatusCode();
                var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var asset = json.RootElement.GetProperty("assets").EnumerateArray()
                    .First(a => a.GetProperty("name").GetString()!.StartsWith("melon_data", StringComparison.OrdinalIgnoreCase));
                var assetUrl = asset.GetProperty("browser_download_url").GetString()!;
                using var download = await _http.GetAsync(assetUrl);
                download.EnsureSuccessStatusCode();
                await using var stream = File.Create(dataPath);
                await download.Content.CopyToAsync(stream);
            }

            SetStatus("Running LemonLoader patch pipeline…");
            var args = new PatchArguments(
                copiedBase, copiedLib, extras, output, Path.Combine(work, "temp"),
                dataPath, Path.Combine(work, "unity-dependencies.zip"),
                null, GamePackage, split);
            var patcher = new Patcher(args, this);
            var success = await Task.Run(patcher.Run);
            if (!success) throw new InvalidOperationException("LemonLoader patching failed. See the log above.");
            var outputs = Directory.GetFiles(output, "*.apk");
            SetStatus("Patch complete. Output files:\n" + string.Join("\n", outputs) +
                "\n\nThese APKs are signed with the patcher's key. They cannot update the original Play Store-signed app in place. Polus has left the original installation untouched.");
        }
        catch (Exception ex)
        {
            SetStatus("Patch failed: " + ex);
        }
        finally
        {
            _patch.Enabled = true;
        }
    }

    private static async Task<string> CopyToPrivateStorage(string source, string directory)
    {
        var target = Path.Combine(directory, Path.GetFileName(source));
        await using var from = File.OpenRead(source);
        await using var to = File.Create(target);
        await from.CopyToAsync(to);
        return target;
    }

    private void SetStatus(string message)
    {
        RunOnUiThread(() => _status.Text = message);
    }

    public void Log(string message) => SetStatus(message);
}
