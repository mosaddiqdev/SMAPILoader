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
    private Dock _dock = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Instance = this;
        base.OnCreate(savedInstanceState);

        Platform.Init(this, savedInstanceState);
        ActivityTool.Init(this);

        if (AssertRequirement() == false)
            return;

        // The activity is rebuilt when the phone rotates. Only a fresh start plays the entrance
        // and may auto-launch the game.
        bool fresh = savedInstanceState == null;
        SetupPages(fresh);
        AppCompatDelegate.DefaultNightMode = AppCompatDelegate.ModeNightYes;

        if (fresh)
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

    private void SetupPages(bool playEntrance)
    {
        // draw behind the system bars; Shell pads the content back inside them
        WindowCompat.SetDecorFitsSystemWindows(Window, false);

        var shell = new Shell(this);
        var pages = new FrameLayout(this);
        _sheet = new SheetHost(this);

        _dock = new Dock(this, new[] { "Home", "Mods" }, new[] { "ic_home", "ic_grid" });
        _home = new HomePage(this, () => _dock.Select(1));
        _mods = new ModsPage(this, _sheet);
        pages.AddView(_home, new FrameLayout.LayoutParams(Ui.Match, Ui.Match));
        pages.AddView(_mods, new FrameLayout.LayoutParams(Ui.Match, Ui.Match));

        // Home says what the key should read (Play, Install SMAPI, Starting...), but only while it is the page on screen
        _home.KeyChanged += (label, busy) =>
        {
            if (_dock.Current == 0)
                _dock.Key.Set(label, busy);
        };

        _dock.Key.Click += (_, _) =>
        {
            if (_dock.Current == 0)
                _home.Play();
            else
                _mods.Add();
        };

        _dock.Picked += index =>
        {
            _sheet.Hide();
            View show = index == 0 ? _home : _mods;
            View hide = index == 0 ? _mods : _home;

            // a short crossfade instead of a hard cut
            if (show.Visibility != ViewStates.Visible)
            {
                show.Alpha = 0f;
                show.Visibility = ViewStates.Visible;
                show.Animate().Alpha(1f).SetDuration(150).Start();
            }
            hide.Visibility = ViewStates.Gone;

            if (index == 0)
            {
                _home.Refresh();
            }
            else
            {
                _mods.Reload();
                _dock.Key.Set("Add mods", false);
            }
        };

        shell.Body.AddView(pages, new LinearLayout.LayoutParams(Ui.Match, 0, 1f));
        shell.Body.AddView(_dock, new LinearLayout.LayoutParams(Ui.Match, Ui.Wrap));
        shell.AddView(_sheet, new FrameLayout.LayoutParams(Ui.Match, Ui.Match));
        SetContentView(shell);
        _dock.Select(0);

        if (playEntrance)
            _home.Enter();
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
