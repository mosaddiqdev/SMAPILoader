using Android.App;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using SMAPIGameLoader.Tool;
using System;
using Xamarin.Essentials;

namespace SMAPIGameLoader.Launcher;

// The interface is being redesigned from zero. Until it lands this is a bare screen that can still launch the game.
[Activity(
    Label = "SMAPI Launcher",
    MainLauncher = true,
    Theme = "@style/Theme.Launcher",
    AlwaysRetainTaskState = true,
    LaunchMode = LaunchMode.SingleInstance,
    ScreenOrientation = ScreenOrientation.Unspecified
)]
public class LauncherActivity : AppCompatActivity
{
    public static LauncherActivity Instance { get; private set; }

    private static bool IsDeviceSupport => IntPtr.Size == 8;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Instance = this;
        base.OnCreate(savedInstanceState);

        Platform.Init(this, savedInstanceState);
        ActivityTool.Init(this);

        if (AssertRequirement() == false)
            return;

        var root = new FrameLayout(this);
        root.SetBackgroundColor(Color.ParseColor("#0F0F10"));
        var launch = new Button(this) { Text = "Launch game" };
        launch.Click += (_, _) => OnClickStartGame();
        root.AddView(launch, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
        {
            Gravity = GravityFlags.Center
        });
        SetContentView(root);

        AppCompatDelegate.DefaultNightMode = AppCompatDelegate.ModeNightYes;

        // The activity is rebuilt when the phone rotates. Only a fresh start may auto-launch the game.
        if (savedInstanceState == null)
            ProcessAdbExtras();
    }

    private void ProcessAdbExtras()
    {
        if (AdbExtraTool.IsClickStartGame(this))
            OnClickStartGame();
    }

    private static bool AssetGameVerify()
    {
        try
        {
            if (StardewApkTool.IsInstalled == false)
            {
                if (StardewApkTool.CurrentPackageInfo != null)
                    ToastNotifyTool.Notify("Stardew Valley is installed, but its split APKs are incomplete. Reinstall the game.");
                else
                    ToastNotifyTool.Notify("Stardew Valley isn’t installed.");

                return false;
            }
        }
        catch (Exception ex)
        {
            ToastNotifyTool.Notify("err;" + ex);
            return false;
        }

        return true;
    }

    private bool AssertRequirement()
    {
        if (IsDeviceSupport is false)
        {
            ToastNotifyTool.Notify("This device is 32-bit, which the launcher doesn’t support.");
            Finish();
            return false;
        }

        if (AssetGameVerify() == false)
        {
            Finish();
            return false;
        }

        return true;
    }

    public void OnClickStartGame()
    {
        Console.WriteLine("On click start game");
        EntryGame.LaunchGameActivity(this);
        Console.WriteLine("done continue UI runner");
    }
}
