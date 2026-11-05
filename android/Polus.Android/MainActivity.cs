using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Android.Webkit;
using MelonLoader.Installer.Core;

namespace Polus.Android;

[Activity(Label = "Polus", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity, IPatchLogger
{
    private const string GamePackage = "com.innersloth.spacemafia";
    private WebView _webView = null!;
    private string? _outputDirectory;
    private readonly HttpClient _http = new();

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        BuildUi();
        RefreshGameStatus();
    }

    private void BuildUi()
    {
        _webView = new WebView(this);
        _webView.Settings.JavaScriptEnabled = true;
        _webView.Settings.DomStorageEnabled = true;
        _webView.SetWebViewClient(new WebViewClient());
        _webView.AddJavascriptInterface(new PolusAndroidBridge(this), "AndroidBridge");
        SetContentView(_webView);
        _webView.LoadUrl("file:///android_asset/patcher/index.html");
        _webView.PostDelayed(() =>
        {
            Evaluate("window.Polus && window.Polus.platform('android')");
            RefreshGameStatus();
        }, 250);
    }

    internal void HandleUiAction(string action)
    {
        switch (action)
        {
            case "detect": RefreshGameStatus(); break;
            case "patch": _ = PatchInstalledGame(); break;
            case "install": ConfirmReplaceGame(); break;
        }
    }

    private void Evaluate(string script)
    {
        if (_webView == null) return;
        RunOnUiThread(() => _webView.EvaluateJavascript(script, null));
    }

    private void SetInstallEnabled(bool enabled) =>
        Evaluate($"window.Polus && window.Polus.installEnabled({enabled.ToString().ToLowerInvariant()})");

    private void RefreshGameStatus()
    {
        try
        {
            var info = PackageManager!.GetApplicationInfo(GamePackage, 0);
            var paths = GetApkPaths(info);
            SetStatus($"Found Among Us. APK parts: {paths.Length}. Device ABI: {string.Join(", ", Build.SupportedAbis ?? Array.Empty<string>())}");
        }
        catch (PackageManager.NameNotFoundException)
        {
            SetStatus("Among Us is not installed. Install the official game first.");
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
        Evaluate("window.Polus && window.Polus.busy(true)");
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
                var extraPaths = new List<string>();
                foreach (var extra in paths.Where(p => p != baseApk && p != armSplit))
                    extraPaths.Add(await CopyToPrivateStorage(extra, input));
                extras = extraPaths.ToArray();
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

            var modAssembly = ExtractEmbeddedAssembly("Polus.Android.Polus.AndroidMod.dll", work);
            var roleCoreAssembly = ExtractEmbeddedAssembly("Polus.Android.Polus.RoleCore.dll", work);

            SetStatus("Running LemonLoader patch pipeline and bundling Polus Android mod…");
            var args = new PatchArguments(
                copiedBase, copiedLib, extras, output, Path.Combine(work, "temp"),
                dataPath, Path.Combine(work, "unity-dependencies.zip"),
                null, GamePackage, split, new[] { modAssembly, roleCoreAssembly });
            var patcher = new Patcher(args, this);
            var success = await Task.Run(patcher.Run);
            if (!success) throw new InvalidOperationException("LemonLoader patching failed. See the log above.");
            var outputs = Directory.GetFiles(output, "*.apk");
            _outputDirectory = output;
            SetInstallEnabled(outputs.Length > 0 && outputs.Any(p => Path.GetFileName(p).Equals("base.apk", StringComparison.OrdinalIgnoreCase)));
            SetStatus("Patch complete. Output files:\n" + string.Join("\n", outputs) +
                "\n\nThese APKs are signed with the patcher's key. They cannot update the original Play Store-signed app in place. Polus has left the original installation untouched.");
        }
        catch (Exception ex)
        {
            SetStatus("Patch failed: " + ex);
        }
        finally
        {
            Evaluate("window.Polus && window.Polus.busy(false)");
        }
    }

    private void ConfirmReplaceGame()
    {
        new AlertDialog.Builder(this)
            .SetTitle("Replace Among Us?")
            .SetMessage("Polus must remove the currently installed Play Store copy before Android can install the differently signed patched build. Uninstalling can erase local game data. Back up anything important first. Android will show a separate uninstall confirmation, followed by its install confirmation.")
            .SetNegativeButton("Cancel", (_, _) => { })
            .SetPositiveButton("Continue", (_, _) =>
            {
                var delete = new Intent(Intent.ActionDelete);
                delete.SetData(global::Android.Net.Uri.Parse("package:" + GamePackage));
                StartActivityForResult(delete, 9401);
            })
            .Show();
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != 9401) return;
        if (IsGameInstalled())
        {
            SetStatus("Among Us is still installed. The uninstall was cancelled; the patched build was not installed.");
            return;
        }
        _ = InstallPatchedApks();
    }

    private bool IsGameInstalled()
    {
        try { PackageManager!.GetApplicationInfo(GamePackage, 0); return true; }
        catch (PackageManager.NameNotFoundException) { return false; }
    }

    private async Task InstallPatchedApks()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_outputDirectory) || !Directory.Exists(_outputDirectory))
                throw new InvalidOperationException("The patched APK output folder is missing. Patch the game again.");
            var apks = Directory.GetFiles(_outputDirectory, "*.apk")
                .OrderBy(p => Path.GetFileName(p).Equals("base.apk", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToArray();
            if (apks.Length == 0 || !Path.GetFileName(apks[0]).Equals("base.apk", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The patched base.apk is missing.");

            var installer = PackageManager!.PackageInstaller;
            var parameters = new PackageInstaller.SessionParams(PackageInstallMode.FullInstall);
            parameters.SetAppPackageName(GamePackage);
            var sessionId = installer.CreateSession(parameters);
            using (var session = installer.OpenSession(sessionId))
            {
                foreach (var apk in apks)
                {
                    await using var input = File.OpenRead(apk);
                    using var output = session.OpenWrite(Path.GetFileName(apk), 0, input.Length);
                    await input.CopyToAsync(output);
                    session.Fsync(output);
                }

                var callback = new Intent(this, typeof(PolusInstallStatusReceiver));
                callback.SetAction(PolusInstallStatusReceiver.Action);
                var pending = PendingIntent.GetBroadcast(this, 9402, callback,
                    PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Mutable);
                session.Commit(pending.IntentSender);
            }
            SetStatus("Android's package installer has been opened. Approve the install prompt to finish.");
        }
        catch (Exception ex)
        {
            SetStatus("Install could not start: " + ex.Message);
        }
    }

    private string ExtractEmbeddedAssembly(string resourceName, string directory)
    {
        using var resource = GetType().Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Missing embedded Polus runtime assembly: " + resourceName);
        var destination = Path.Combine(directory, Path.GetFileName(resourceName));
        using var output = File.Create(destination);
        resource.CopyTo(output);
        return destination;
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
        Evaluate($"window.Polus && window.Polus.status({System.Text.Json.JsonSerializer.Serialize(message)}, false)");
    }

    public void Log(string message)
    {
        Evaluate($"window.Polus && window.Polus.log({System.Text.Json.JsonSerializer.Serialize(message)})");
        SetStatus(message);
    }
}


namespace Polus.Android;

[Android.Runtime.Preserve(AllMembers = true)]
public sealed class PolusAndroidBridge
{
    private readonly MainActivity _activity;
    public PolusAndroidBridge(MainActivity activity) => _activity = activity;

    [JavascriptInterface]
    [Export("action")]
    public void Action(string action) => _activity.RunOnUiThread(() => _activity.HandleUiAction(action));
}
