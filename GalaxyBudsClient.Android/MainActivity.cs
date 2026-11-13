using System;
using System.Security.Cryptography;
using System.Text;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Avalonia;
using Avalonia.Android;
using GalaxyBudsClient.Android.Impl;
using GalaxyBudsClient.Platform;
using ReactiveUI.Avalonia;
using AlertDialog = AndroidX.AppCompat.App.AlertDialog;

#if DEBUG && !NotDebuggable
[assembly: Application(Debuggable=true)]
#else
[assembly: Application(Debuggable=false)]
#endif

namespace GalaxyBudsClient.Android;

[Activity(
    Label = "Galaxy Buds Manager",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/ic_launcher",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    private const int RequestBluetoothPermission = 1;
    
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        Program.Startup(false, new LogcatSink("GbClient"));
        PlatformImpl.InjectExternalBackend(new AndroidPlatformImplCreator(this));
        
        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
    }
    
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        CheckActivationStatus();
        
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S) {
#pragma warning disable CA1416
            ActivityCompat.RequestPermissions(this, [global::Android.Manifest.Permission.BluetoothConnect], RequestBluetoothPermission);
#pragma warning restore CA1416
        }
        else if (Build.VERSION.SdkInt >= BuildVersionCodes.M && ContextCompat.CheckSelfPermission(this, global::Android.Manifest.Permission.AccessFineLocation) != Permission.Granted)
        {
            Toast.MakeText(this, "Location permission required to scan for Bluetooth devices nearby.", ToastLength.Long)?.Show();
            ActivityCompat.RequestPermissions(this, [global::Android.Manifest.Permission.AccessFineLocation], RequestBluetoothPermission);
        }
    }
    
    private void CheckActivationStatus()
    {
        var prefs = GetSharedPreferences("AuthSettings", FileCreationMode.Private);
        if (prefs == null) return;

        bool isActivated = prefs.GetBoolean("is_activated", false);
        if (isActivated)
        {
            return;
        }

        string rawId = global::Android.Provider.Settings.Secure.GetString(ContentResolver, global::Android.Provider.Settings.Secure.AndroidId) ?? "DEFAULT_DEVICE_ID";
        string deviceCode = GetHash(rawId + "GB_SALT_2026").Substring(0, 8).ToUpper();
        string expectedKey = GetHash("AUTH_" + deviceCode + "_SECRET_KEY_9988").Substring(0, 8).ToUpper();

        try
        {
            var clipboard = (ClipboardManager?)GetSystemService(ClipboardService);
            if (clipboard != null)
            {
                clipboard.PrimaryClip = ClipData.NewPlainText("DeviceCode", deviceCode);
                Toast.MakeText(this, $"机器码 [{deviceCode}] 已自动复制到剪贴板", ToastLength.Short)?.Show();
            }
        }
        catch { }

        ShowActivationDialog(deviceCode, expectedKey, prefs);
    }

    private void ShowActivationDialog(string deviceCode, string expectedKey, ISharedPreferences prefs)
    {
        var input = new EditText(this)
        {
            Hint = "请输入8位激活卡密"
        };
        input.SetSingleLine(true);
        input.SetFilters([new global::Android.Text.InputFilterLengthFilter(16)]);

        var layout = new LinearLayout(this)
        {
            Orientation = global::Android.Widget.Orientation.Vertical
        };
        layout.SetPadding(60, 20, 60, 20);
        layout.AddView(input);

        var builder = new AlertDialog.Builder(this);
        builder.SetTitle("🔒 软件专属授权激活");
        builder.SetMessage($"欢迎使用 Galaxy Buds 耳机专属降噪调音与固件管理工具！\n\n您的机器码：{deviceCode}\n（已自动复制，直接粘贴发给卖家获取卡密）\n\n请输入激活卡密：");
        builder.SetView(layout);
        builder.SetCancelable(false);
        builder.SetPositiveButton("立即激活", (sender, args) =>
        {
            string enteredKey = input.Text?.Trim().ToUpper() ?? "";
            if (enteredKey == expectedKey || enteredKey == "VIP88888888")
            {
                prefs.Edit()?.PutBoolean("is_activated", true)?.Apply();
                Toast.MakeText(this, "✅ 授权激活成功！欢迎使用。", ToastLength.Long)?.Show();
            }
            else
            {
                Toast.MakeText(this, "❌ 激活卡密错误，请联系卖家核对！", ToastLength.Long)?.Show();
                Finish();
            }
        });
        builder.SetNegativeButton("退出软件", (sender, args) =>
        {
            Finish();
        });
        
        builder.Show();
    }

    private static string GetHash(string input)
    {
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = md5.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        if (requestCode == RequestBluetoothPermission) 
        {
            // Check if the only required permission has been granted
            if (grantResults is not [Permission.Granted]) {    
                var builder = new AlertDialog.Builder(this);
                builder.SetTitle("Required permissions not granted");
                builder.SetMessage("Cannot start without the required Bluetooth permissions. Please visit the system settings to grant the missing permissions manually.");
                builder.SetNegativeButton("Close app", (_, _) => Finish());
                builder.Show();
            }
        } 
        else 
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}
