using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Widget;

namespace Polus.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter(new[] { Action })]
public sealed class PolusInstallStatusReceiver : BroadcastReceiver
{
    public const string Action = "com.tvmanisback.poluscompanion.INSTALL_STATUS";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;
        var status = intent.GetIntExtra(PackageInstaller.ExtraStatus, 1);
        var message = intent.GetStringExtra(PackageInstaller.ExtraStatusMessage) ?? "No installer details were provided.";

        if (status == -1)
        {
            var confirmation = intent.GetParcelableExtra("android.intent.extra.INTENT") as Intent;
            if (confirmation != null)
            {
                confirmation.AddFlags(ActivityFlags.NewTask);
                context.StartActivity(confirmation);
            }
            return;
        }

        var text = status == 0
            ? "Polus-patched Among Us installed. Open the game to choose Vanilla or Polus Modded."
            : "Among Us install failed: " + message;
        Toast.MakeText(context, text, ToastLength.Long)?.Show();
    }
}
