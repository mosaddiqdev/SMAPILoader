using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using System;
using System.IO;
using System.Linq;
using Xamarin.Essentials;

namespace SMAPIGameLoader.Launcher.Pages;

// One line in a settings list: status dot, title, optional note, value on the right.
internal sealed class SettingRow : LinearLayout
{
    public TextView Value { get; }
    readonly TextView _note;
    readonly View _dot;

    public SettingRow(Context ctx, string title) : base(ctx)
    {
        Orientation = Orientation.Horizontal;
        SetGravity(GravityFlags.CenterVertical);
        SetPadding(Ui.Dp(ctx, 16), Ui.Dp(ctx, 14), Ui.Dp(ctx, 16), Ui.Dp(ctx, 14));

        _dot = Ui.Dot(ctx, Tokens.Faint);
        AddView(_dot, new LayoutParams(Ui.Dp(ctx, 8), Ui.Dp(ctx, 8)) { RightMargin = Ui.Dp(ctx, 14) });

        var left = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        left.AddView(Ui.Label(ctx, title, 16, Tokens.Text, medium: true));
        _note = Ui.Label(ctx, "", 13, Tokens.Muted);
        _note.Visibility = ViewStates.Gone;
        left.AddView(_note, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 3) });
        AddView(left, new LayoutParams(0, Ui.Wrap, 1f));

        Value = Ui.Label(ctx, "", 14, Tokens.Text, mono: true);
        AddView(Value);
    }

    public void SetNote(string text)
    {
        _note.Text = text ?? "";
        _note.Visibility = string.IsNullOrEmpty(text) ? ViewStates.Gone : ViewStates.Visible;
    }

    public void SetStatus(Color color) => Ui.SetDotColor(_dot, color);

    // Rows that open something get a chevron. Rows that flip a setting in place don't.
    public void OnTap(Action onTap, bool chevron = true)
    {
        Ui.MakeTappable(this, onTap);
        if (!chevron)
            return;

        AddView(Ui.IconView(Context, "ic_chevron", Tokens.Faint, 18),
            new LayoutParams(Ui.Dp(Context, 18), Ui.Dp(Context, 18)) { LeftMargin = Ui.Dp(Context, 8) });
    }
}

internal sealed class PlayPage : LinearLayout
{
    readonly Context _ctx;
    readonly SheetHost _sheet;
    readonly TextView _headline;
    readonly TextView _summary;
    readonly TextView _launch;
    readonly TextView _footer;
    readonly SettingRow _game;
    readonly SettingRow _smapi;
    readonly SettingRow _mods;
    readonly SettingRow _log;
    readonly SettingRow _button;

    // the last log only gets re-read when the file actually changed
    DateTime _logWrite;
    long _logLength = -1;
    int _logWarnings;
    int _logErrors;

    public PlayPage(Context ctx, SheetHost sheet, Action openMods) : base(ctx)
    {
        _ctx = ctx;
        _sheet = sheet;
        Orientation = Orientation.Vertical;

        var scroll = new ScrollView(ctx) { VerticalScrollBarEnabled = false };
        var content = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        content.SetPadding(Ui.Dp(ctx, 20), Ui.Dp(ctx, 28), Ui.Dp(ctx, 20), Ui.Dp(ctx, 24));
        scroll.AddView(content);
        AddView(scroll, new LayoutParams(Ui.Match, 0, 1f));

        var eyebrow = Ui.Label(ctx, "SMAPI LAUNCHER", 12, Tokens.Muted, mono: true);
        eyebrow.LetterSpacing = 0.12f;
        content.AddView(eyebrow);

        _headline = Ui.Label(ctx, "", 30, Tokens.Text, medium: true);
        content.AddView(_headline, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 12) });

        _summary = Ui.Label(ctx, "", 15, Tokens.Muted);
        _summary.SetLineSpacing(0, 1.15f);
        content.AddView(_summary, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 8) });

        content.AddView(SectionLabel("SETUP", 32));
        _game = new SettingRow(ctx, "Stardew Valley");
        _smapi = new SettingRow(ctx, "SMAPI");
        _mods = new SettingRow(ctx, "Mods");
        _smapi.OnTap(() =>
        {
            if (SMAPIInstaller.IsInstalled)
                ShowSmapiSheet();
            else
                InstallSmapi();
        });
        _mods.OnTap(openMods);
        content.AddView(Group(_game, _smapi, _mods), new LayoutParams(Ui.Match, Ui.Wrap));

        content.AddView(SectionLabel("LOGS", 28));
        _log = new SettingRow(ctx, "Last log");
        _button = new SettingRow(ctx, "In-game log button");
        _log.OnTap(ShowLogSheet);
        _button.OnTap(() =>
        {
            Preferences.Set(LogOverlay.PrefKey, !LogOverlay.ButtonEnabled);
            Refresh();
        }, chevron: false);
        content.AddView(Group(_log, _button), new LayoutParams(Ui.Match, Ui.Wrap));

        _footer = Ui.Label(ctx, "", 11, Tokens.Faint, mono: true);
        _footer.SetLineSpacing(0, 1.3f);
        content.AddView(_footer, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 20) });

        var bar = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        bar.SetPadding(Ui.Dp(ctx, 20), Ui.Dp(ctx, 8), Ui.Dp(ctx, 20), Ui.Dp(ctx, 16));
        _launch = Ui.Button(ctx, "Launch game", Ui.Kind.Primary);
        _launch.Click += (_, _) => LauncherActivity.Instance.OnClickStartGame();
        bar.AddView(_launch, new LayoutParams(Ui.Match, Ui.Wrap));
        AddView(bar, new LayoutParams(Ui.Match, Ui.Wrap));

        Refresh();
    }

    TextView SectionLabel(string text, int topDp)
    {
        var label = Ui.Label(_ctx, text, 12, Tokens.Muted, mono: true);
        label.LetterSpacing = 0.12f;
        label.LayoutParameters = new LayoutParams(Ui.Match, Ui.Wrap)
        {
            TopMargin = Ui.Dp(_ctx, topDp),
            BottomMargin = Ui.Dp(_ctx, 10)
        };
        return label;
    }

    LinearLayout Group(params SettingRow[] rows)
    {
        var panel = Ui.Panel(_ctx);
        for (int i = 0; i < rows.Length; i++)
        {
            if (i > 0)
                panel.AddView(Ui.Divider(_ctx));
            panel.AddView(rows[i]);
        }
        return panel;
    }

    public void Refresh()
    {
        try
        {
            bool smapi = SMAPIInstaller.IsInstalled;
            var mods = ModLibrary.Scan();
            int on = mods.Count(m => m.Enabled);
            var version = StardewApkTool.CurrentGameVersion;
            bool tested = StardewApkTool.IsGameVersionSupport;

            _headline.Text = smapi ? "Ready to play" : "SMAPI isn’t installed";
            _summary.Text = smapi
                ? $"Stardew Valley {version}, SMAPI {SmapiVersion()}, {on} {(on == 1 ? "mod" : "mods")} on."
                : "Install SMAPI to launch the game with mods.";

            bool split = StardewApkTool.CurrentPackageInfo?.ApplicationInfo?.SplitSourceDirs?.Count > 0;
            _game.Value.Text = version.ToString();
            _game.SetNote((split ? "Split APK install" : "Single APK install") + (tested ? "" : ", untested version"));
            _game.SetStatus(tested ? Tokens.Green : Tokens.Gold);

            if (smapi)
            {
                _smapi.Value.Text = SmapiVersion();
                _smapi.Value.SetTextColor(Tokens.Text);
                _smapi.SetNote("Built " + BuildDate(SMAPIInstaller.GetBuildCode()));
                _smapi.SetStatus(Tokens.Green);
            }
            else
            {
                _smapi.Value.Text = "Install";
                _smapi.Value.SetTextColor(Tokens.Gold);
                _smapi.SetNote("Pick the SMAPI .zip made for Android");
                _smapi.SetStatus(Tokens.Red);
            }

            _mods.Value.Text = mods.Count == 0 ? "Add" : $"{on} on";
            _mods.Value.SetTextColor(mods.Count == 0 ? Tokens.Gold : Tokens.Text);
            _mods.SetNote(mods.Count == 0 ? "Nothing installed yet" : $"{mods.Count} installed");
            _mods.SetStatus(on > 0 ? Tokens.Green : Tokens.Faint);

            RefreshLogRows();

            _launch.Text = smapi ? "Launch game" : "Install SMAPI first";
            Ui.SetAvailable(_launch, smapi);

            _footer.Text = $"Launcher {AppInfo.VersionString}, build {BuildDate(long.Parse(AppInfo.BuildString))}\nTested on game {StardewApkTool.GameVersionSupport} and newer";
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    void RefreshLogRows()
    {
        var file = new FileInfo(LogSession.LogPath);
        if (!file.Exists)
        {
            _log.Value.Text = "None";
            _log.Value.SetTextColor(Tokens.Muted);
            _log.SetNote("Launch the game once to create one");
            _log.SetStatus(Tokens.Faint);
        }
        else
        {
            if (file.LastWriteTimeUtc != _logWrite || file.Length != _logLength)
            {
                var session = new LogSession();
                session.LoadAll();
                _logWarnings = session.Warnings;
                _logErrors = session.Errors;
                _logWrite = file.LastWriteTimeUtc;
                _logLength = file.Length;
            }

            if (_logErrors > 0)
            {
                _log.Value.Text = _logErrors == 1 ? "1 error" : $"{_logErrors} errors";
                _log.Value.SetTextColor(Tokens.Red);
                _log.SetStatus(Tokens.Red);
            }
            else if (_logWarnings > 0)
            {
                _log.Value.Text = _logWarnings == 1 ? "1 warning" : $"{_logWarnings} warnings";
                _log.Value.SetTextColor(Tokens.Gold);
                _log.SetStatus(Tokens.Gold);
            }
            else
            {
                _log.Value.Text = "Clean";
                _log.Value.SetTextColor(Tokens.Green);
                _log.SetStatus(Tokens.Green);
            }
            _log.SetNote("Last run, " + file.LastWriteTime.ToString("d MMM, HH:mm"));
        }

        bool buttonOn = LogOverlay.ButtonEnabled;
        _button.Value.Text = buttonOn ? "On" : "Off";
        _button.Value.SetTextColor(buttonOn ? Tokens.Text : Tokens.Muted);
        _button.SetNote("A small button you can drag while playing. Tap it to read the log.");
        _button.SetStatus(buttonOn ? Tokens.Green : Tokens.Faint);
    }

    static string SmapiVersion()
    {
        try { return SMAPIInstaller.GetCurrentVersion().ToString(); }
        catch { return "unknown"; }
    }

    static string BuildDate(long unixSeconds)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("d MMM yyyy, HH:mm");
    }

    void InstallSmapi()
    {
        SMAPIInstaller.OnInstalledSMAPI = Refresh;
        SMAPIInstaller.OnClickInstallSMAPIZip(this, EventArgs.Empty);
    }

    LayoutParams Gap(int dp) => new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(_ctx, dp) };

    void ShowLogSheet()
    {
        var session = new LogSession();
        session.LoadAll();

        var view = new LogView(_ctx, session, inOverlay: false)
        {
            EmptyText = "No log yet. Launch the game once and it shows up here."
        };
        view.LayoutParameters = new FrameLayout.LayoutParams(Ui.Match, (int)(_ctx.Resources.DisplayMetrics.HeightPixels * 0.78f));
        _sheet.Show(view);
        view.Refresh();
    }

    void ShowSmapiSheet()
    {
        var body = Ui.SheetBody(_ctx);
        body.AddView(Ui.Label(_ctx, "SMAPI", 22, Tokens.Text, medium: true));
        body.AddView(Ui.Label(_ctx, "Version " + SmapiVersion(), 14, Tokens.Muted, mono: true), Gap(6));

        var details = Ui.Panel(_ctx);
        details.SetPadding(Ui.Dp(_ctx, 14), Ui.Dp(_ctx, 12), Ui.Dp(_ctx, 14), Ui.Dp(_ctx, 12));
        details.AddView(Ui.Field(_ctx, "Built", BuildDate(SMAPIInstaller.GetBuildCode())));
        details.AddView(Ui.Field(_ctx, "File", SMAPIInstaller.GetInstallFilePath), Gap(12));
        body.AddView(details, Gap(18));

        var update = Ui.Button(_ctx, "Update from zip");
        update.Click += (_, _) =>
        {
            _sheet.Hide();
            InstallSmapi();
        };
        var remove = Ui.Button(_ctx, "Remove", Ui.Kind.Danger);
        remove.Click += (_, _) =>
        {
            _sheet.Hide();
            ConfirmRemove();
        };

        var actions = new LinearLayout(_ctx) { Orientation = Orientation.Horizontal };
        actions.AddView(update, new LayoutParams(0, Ui.Wrap, 1f) { RightMargin = Ui.Dp(_ctx, 12) });
        actions.AddView(remove, new LayoutParams(0, Ui.Wrap, 1f));
        body.AddView(actions, Gap(20));

        _sheet.Show(body);
    }

    void ConfirmRemove()
    {
        new AlertDialog.Builder(_ctx)
            .SetTitle("Remove SMAPI?")
            .SetMessage("Your mods and saves stay where they are. You won’t be able to launch until SMAPI is installed again.")
            .SetPositiveButton("Remove", (s, e) =>
            {
                SMAPIInstaller.UninstallSMAPI();
                Refresh();
            })
            .SetNegativeButton("Cancel", (s, e) => { })
            .Show();
    }
}
