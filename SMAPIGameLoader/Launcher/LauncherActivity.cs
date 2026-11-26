using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Core.View;
using SMAPIGameLoader.Tool;
using System;
using Xamarin.Essentials;

namespace SMAPIGameLoader.Launcher;

// Orientation follows the phone. The game itself is landscape-only (see SMAPIActivity).
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

    private HomePage _home = null!;
    private ModsPage _mods = null!;
    private SheetHost _sheet = null!;
    private NavBar _nav = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Instance = this;
        base.OnCreate(savedInstanceState);

        Platform.Init(this, savedInstanceState);
        ActivityTool.Init(this);

        if (AssertRequirement() == false)
            return;

        SetupPages();
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

    private void SetupPages()
    {
        // draw behind the system bars; Shell pads the content back inside them
        WindowCompat.SetDecorFitsSystemWindows(Window, false);

        var shell = new Shell(this);
        var pages = new FrameLayout(this);
        _sheet = new SheetHost(this);

        _nav = new NavBar(this, new[] { "Home", "Mods" }, new[] { "ic_home", "ic_grid" });
        _home = new HomePage(this, () => _nav.Select(1));
        _mods = new ModsPage(this, _sheet);
        pages.AddView(_home, new FrameLayout.LayoutParams(Ui.Match, Ui.Match));
        pages.AddView(_mods, new FrameLayout.LayoutParams(Ui.Match, Ui.Match));

        _nav.Picked += index =>
        {
            _sheet.Hide();
            _home.Visibility = index == 0 ? ViewStates.Visible : ViewStates.Gone;
            _mods.Visibility = index == 1 ? ViewStates.Visible : ViewStates.Gone;
            if (index == 0)
                _home.Refresh();
            else
                _mods.Reload();
        };

        shell.Body.AddView(pages, new LinearLayout.LayoutParams(Ui.Match, 0, 1f));
        shell.Body.AddView(_nav, new LinearLayout.LayoutParams(Ui.Match, Ui.Wrap));
        shell.AddView(_sheet, new FrameLayout.LayoutParams(Ui.Match, Ui.Match));
        SetContentView(shell);
        _nav.Select(0);
    }

    protected override void OnResume()
    {
        base.OnResume();
        _home?.Refresh();
    }

    public override void OnBackPressed()
    {
        if (_sheet != null && _sheet.IsOpen)
            _sheet.Hide();
        else
            base.OnBackPressed();
    }

    public void OnClickStartGame()
    {
        Console.WriteLine("On click start game");
        EntryGame.LaunchGameActivity(this);
        Console.WriteLine("done continue UI runner");
    }
}
