using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Core.View;
using SMAPIGameLoader.Launcher.Pages;
using SMAPIGameLoader.Tool;
using System;
using Xamarin.Essentials;

namespace SMAPIGameLoader.Launcher;

[Activity(
    Label = "SMAPI Launcher",
    MainLauncher = true,
    Theme = "@style/Theme.Charcoal",
    AlwaysRetainTaskState = true,
    LaunchMode = LaunchMode.SingleInstance,
    ScreenOrientation = ScreenOrientation.FullSensor
)]
public class LauncherActivity : AppCompatActivity
{
    public static LauncherActivity Instance { get; private set; }

    private static bool IsDeviceSupport => IntPtr.Size == 8;

    private Shell _shell = null!;
    private SheetHost _sheet = null!;
    private TabBar _tabs = null!;
    private PlayPage _playPage = null!;
    private ModsPage _modsPage = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Instance = this;
        base.OnCreate(savedInstanceState);

        Platform.Init(this, savedInstanceState);
        ActivityTool.Init(this);

        if (AssertRequirement() == false)
            return;

        SetupLauncherPage();
        SetDarkMode();

        //run utils scripts
        ProcessAdbExtras();
    }

    private void SetDarkMode()
    {
        AppCompatDelegate.DefaultNightMode = AppCompatDelegate.ModeNightYes;
    }

    /// <summary>
    ///     Receive argument launch activity
    /// </summary>
    private void ProcessAdbExtras()
    {
        if (AdbExtraTool.IsClickStartGame(this))
        {
            OnClickStartGame();
        }
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
        //check if 32bit not support
        if (IsDeviceSupport is false)
        {
            ToastNotifyTool.Notify("This device is 32-bit, which the launcher doesn’t support.");
            Finish();
            return false;
        }

        //Assert Game Requirement
        if (AssetGameVerify() == false)
        {
            Finish();
            return false;
        }

        return true;
    }

    private void SetupLauncherPage()
    {
        // draw behind the system bars; Shell pads the content back inside them
        WindowCompat.SetDecorFitsSystemWindows(Window, false);

        _shell = new Shell(this);
        _sheet = new SheetHost(this);

        var pages = new FrameLayout(this);
        _tabs = new TabBar(this, "Play", "Mods");
        _modsPage = new ModsPage(this, _sheet);
        _playPage = new PlayPage(this, _sheet, () => _tabs.Select(1));
        pages.AddView(_playPage, new FrameLayout.LayoutParams(Ui.Match, Ui.Match));
        pages.AddView(_modsPage, new FrameLayout.LayoutParams(Ui.Match, Ui.Match));

        _tabs.Selected += ShowPage;

        _shell.Body.AddView(pages, new LinearLayout.LayoutParams(Ui.Match, 0, 1f));
        _shell.Body.AddView(_tabs, new LinearLayout.LayoutParams(Ui.Match, Ui.Wrap));
        _shell.AddView(_sheet, new FrameLayout.LayoutParams(Ui.Match, Ui.Match));
        SetContentView(_shell);

        _tabs.Select(0);
    }

    private void ShowPage(int index)
    {
        _sheet.Hide();
        _playPage.Visibility = index == 0 ? ViewStates.Visible : ViewStates.Gone;
        _modsPage.Visibility = index == 1 ? ViewStates.Visible : ViewStates.Gone;

        if (index == 0)
            _playPage.Refresh();
        else
            _modsPage.Reload();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _playPage?.Refresh();
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
