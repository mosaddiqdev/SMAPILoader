using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using AndroidX.AppCompat.App;
using System;
using System.Collections.Generic;
using System.Linq;
using Xamarin.Essentials;

namespace SMAPIGameLoader.Launcher;

internal sealed class HomePage : LinearLayout
{
    const string LastPlayedKey = "last_played";

    readonly Context _ctx;
    readonly Action _openMods;
    readonly TextView _greeting;
    readonly TextView _meta;
    readonly TextView _status;
    readonly TextView _note;
    readonly ImageView _icon;
    readonly LinearLayout _loadoutSection;
    readonly LinearLayout _loadout;
    readonly TextView _smapiNote;
    readonly TextView _smapiValue;
    readonly Keycap _play;
    readonly List<View> _entrance = new();
    bool _launching;

    public HomePage(Context ctx, Action openMods) : base(ctx)
    {
        _ctx = ctx;
        _openMods = openMods;
        Orientation = Orientation.Vertical;

        var scroll = new ScrollView(ctx) { VerticalScrollBarEnabled = false };
        var content = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        content.SetPadding(Ui.Dp(ctx, 22), Ui.Dp(ctx, 24), Ui.Dp(ctx, 22), Ui.Dp(ctx, 16));
        scroll.AddView(content);
        AddView(scroll, new LayoutParams(Ui.Match, 0, 1f));

        // who and what: greeting, title, versions, and the game's own icon
        var identity = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
        var left = new LinearLayout(ctx) { Orientation = Orientation.Vertical };

        _greeting = Ui.Label(ctx, "", 13, Tokens.Muted);
        left.AddView(_greeting);

        var title = Ui.Label(ctx, "Stardew\nValley", 40, Tokens.Text, Ui.Face.DisplayBold);
        title.SetLineSpacing(0, 0.95f);
        left.AddView(title, new LayoutParams(Ui.Wrap, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 10) });

        _meta = Ui.Label(ctx, "", 12, Tokens.Dim, Ui.Face.Mono);
        left.AddView(_meta, new LayoutParams(Ui.Wrap, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 8) });
        identity.AddView(left, new LayoutParams(0, Ui.Wrap, 1f));

        _icon = new ImageView(ctx);
        _icon.Background = Ui.Box(ctx, Tokens.Surface, 13, Tokens.Line);
        _icon.ClipToOutline = true;
        _icon.OutlineProvider = ViewOutlineProvider.Background;
        identity.AddView(_icon, new LayoutParams(Ui.Dp(ctx, 52), Ui.Dp(ctx, 52)));
        content.AddView(identity);

        _status = Ui.Label(ctx, "", 15, Tokens.TextSoft);
        _status.SetLineSpacing(0, 1.15f);
        content.AddView(_status, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 16) });

        // loadout: what will load
        _loadoutSection = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        var header = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
        header.SetGravity(GravityFlags.CenterVertical);
        header.AddView(Ui.Label(ctx, "Loadout", 18, Tokens.TextSoft, Ui.Face.Display), new LayoutParams(0, Ui.Wrap, 1f));
        var manage = Ui.Label(ctx, "Manage", 13, Tokens.Faint);
        manage.SetPadding(Ui.Dp(ctx, 12), Ui.Dp(ctx, 8), 0, Ui.Dp(ctx, 8));
        Ui.MakeTappable(manage, _openMods);
        header.AddView(manage);
        _loadoutSection.AddView(header, new LayoutParams(Ui.Match, Ui.Wrap) { BottomMargin = Ui.Dp(ctx, 4) });

        _loadout = Ui.Panel(ctx);
        _loadoutSection.AddView(_loadout, new LayoutParams(Ui.Match, Ui.Wrap));
        content.AddView(_loadoutSection, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 26) });

        // SMAPI
        var smapi = Ui.Panel(ctx);
        var smapiRow = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
        smapiRow.SetGravity(GravityFlags.CenterVertical);
        smapiRow.SetPadding(Ui.Dp(ctx, 16), Ui.Dp(ctx, 13), Ui.Dp(ctx, 16), Ui.Dp(ctx, 13));
        var smapiText = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        smapiText.AddView(Ui.Label(ctx, "SMAPI", 15, Tokens.Text, Ui.Face.Medium));
        _smapiNote = Ui.Label(ctx, "", 12, Tokens.Dim);
        smapiText.AddView(_smapiNote, new LayoutParams(Ui.Wrap, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 2) });
        smapiRow.AddView(smapiText, new LayoutParams(0, Ui.Wrap, 1f));
        _smapiValue = Ui.Label(ctx, "", 13, Tokens.TextSoft, Ui.Face.Mono);
        smapiRow.AddView(_smapiValue, new LayoutParams(Ui.Wrap, Ui.Wrap) { RightMargin = Ui.Dp(ctx, 8) });
        smapiRow.AddView(Ui.IconView(ctx, "ic_chevron", Tokens.Faint, 16), new LayoutParams(Ui.Dp(ctx, 16), Ui.Dp(ctx, 16)));
        Ui.MakeTappable(smapiRow, OnSmapiTap);
        smapi.AddView(smapiRow);
        content.AddView(smapi, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 12) });

        // pinned at the bottom, where the thumb already is
        var bar = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        bar.SetPadding(Ui.Dp(ctx, 22), Ui.Dp(ctx, 12), Ui.Dp(ctx, 22), Ui.Dp(ctx, 10));
        _play = new Keycap(ctx, 58, 16, 22, Ui.Face.DisplayBold);
        _play.Click += (_, _) => OnPlay();
        bar.AddView(_play, new LayoutParams(Ui.Match, _play.TotalHeight));
        _note = Ui.Label(ctx, "", 12, Tokens.Dim);
        _note.Gravity = GravityFlags.Center;
        bar.AddView(_note, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 10) });
        AddView(bar, new LayoutParams(Ui.Match, Ui.Wrap));

        _entrance.Add(identity);
        _entrance.Add(_status);
        _entrance.Add(_loadoutSection);
        _entrance.Add(smapi);
        _entrance.Add(bar);

        Refresh();
    }

    // Each block rises into place a beat after the one before it. Played once, when the launcher opens.
    public void Enter()
    {
        float rise = Ui.Dp(_ctx, 14);
        for (int i = 0; i < _entrance.Count; i++)
        {
            var view = _entrance[i];
            float target = view == _loadoutSection && !SMAPIInstaller.IsInstalled ? 0.4f : 1f;

            view.Alpha = 0f;
            view.TranslationY = rise;
            view.Animate()
                .Alpha(target)
                .TranslationY(0f)
                .SetStartDelay(i * 55)
                .SetDuration(300)
                .SetInterpolator(new DecelerateInterpolator())
                .Start();
        }
    }

    // Called whenever the launcher comes back to the front, so a finished launch resets the button.
    public void Refresh()
    {
        _launching = false;
        Apply();
    }

    static bool Broken(ModInfo mod) => mod.Compat?.Level == CompatLevel.Incompatible;

    static string Plural(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";

    void Apply()
    {
        try
        {
            bool smapi = SMAPIInstaller.IsInstalled;
            var mods = ModLibrary.Scan();
            var enabled = mods.Where(m => m.Enabled).ToList();
            int broken = enabled.Count(Broken);
            int willLoad = enabled.Count - broken;
            var version = StardewApkTool.CurrentGameVersion;
            string smapiVersion = SmapiVersion();

            _greeting.Text = Greeting();
            _meta.Text = smapi ? $"{version} · SMAPI {smapiVersion}" : $"{version} · SMAPI not installed";
            ShowGameIcon();

            if (_launching)
                _status.Text = $"Starting Stardew Valley with {Plural(willLoad, "mod")}.";
            else if (!smapi)
                _status.Text = "SMAPI isn’t installed yet.";
            else if (mods.Count == 0)
                _status.Text = "Ready. No mods yet.";
            else if (enabled.Count == 0)
                _status.Text = "Ready. All your mods are turned off.";
            else
                _status.Text = $"Ready. {Plural(willLoad, "mod")} will load" + (broken > 0 ? $", {broken} won’t." : ".");

            FillLoadout(mods, enabled);
            _loadoutSection.Alpha = smapi ? 1f : 0.4f;

            if (smapi)
            {
                _smapiValue.Text = smapiVersion;
                _smapiValue.SetTextColor(Tokens.TextSoft);
                _smapiNote.Text = "Built " + BuildDate(SMAPIInstaller.GetBuildCode());
            }
            else
            {
                _smapiValue.Text = "Not installed";
                _smapiValue.SetTextColor(Tokens.Danger);
                _smapiNote.Text = "Needed to run mods";
            }

            if (_launching)
            {
                _play.Set("Starting…", true);
                _note.Text = "This takes a few seconds";
            }
            else if (!smapi)
            {
                _play.Set("Install SMAPI", false);
                _note.Text = "Pick the SMAPI zip made for Android";
            }
            else
            {
                _play.Set("Play", false);
                var last = Preferences.Get(LastPlayedKey, DateTime.MinValue);
                _note.Text = last == DateTime.MinValue ? "Ready when you are" : "Last played " + Ago(last);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    void ShowGameIcon()
    {
        try
        {
            var package = StardewApkTool.CurrentPackageInfo?.PackageName;
            if (package != null)
                _icon.SetImageDrawable(_ctx.PackageManager.GetApplicationIcon(package));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    void FillLoadout(List<ModInfo> all, List<ModInfo> enabled)
    {
        _loadout.RemoveAllViews();

        // problems first, so they never hide behind the "more" row
        var shown = enabled
            .OrderByDescending(Broken)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        foreach (var mod in shown)
        {
            string meta;
            Color color;
            if (Broken(mod))
            {
                meta = "Incompatible · " + (mod.Compat.Problems.FirstOrDefault() ?? "see the Mods tab");
                color = Tokens.Danger;
            }
            else
            {
                var parts = new[] { mod.Author, mod.Version.Length > 0 ? mod.Version : "" };
                meta = string.Join(" · ", parts.Where(p => p.Length > 0));
                color = Tokens.Dim;
            }
            AddLoadoutRow(mod.Name, meta, color, true);
        }

        if (enabled.Count > shown.Count)
            AddLoadoutRow(Plural(enabled.Count - shown.Count, "more mod"), null, Tokens.Dim, false);
        else if (all.Count == 0)
            AddLoadoutRow("No mods yet", "Add some from the Mods tab", Tokens.Dim, true);
        else if (enabled.Count == 0)
            AddLoadoutRow("Everything is off", "Turn mods on from the Mods tab", Tokens.Dim, true);
    }

    void AddLoadoutRow(string title, string meta, Color metaColor, bool strong)
    {
        var row = new LinearLayout(_ctx) { Orientation = Orientation.Horizontal };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(Ui.Dp(_ctx, 16), Ui.Dp(_ctx, 13), Ui.Dp(_ctx, 16), Ui.Dp(_ctx, 13));

        var text = new LinearLayout(_ctx) { Orientation = Orientation.Vertical };
        var name = strong
            ? Ui.Label(_ctx, title, 15, Tokens.Text, Ui.Face.Medium)
            : Ui.Label(_ctx, title, 14, Tokens.Muted);
        name.SetSingleLine(true);
        name.Ellipsize = TextUtils.TruncateAt.End;
        text.AddView(name);

        if (!string.IsNullOrEmpty(meta))
        {
            var line = Ui.Label(_ctx, meta, 12, metaColor);
            line.SetSingleLine(true);
            line.Ellipsize = TextUtils.TruncateAt.End;
            text.AddView(line, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(_ctx, 2) });
        }
        row.AddView(text, new LayoutParams(0, Ui.Wrap, 1f));
        row.AddView(Ui.IconView(_ctx, "ic_chevron", Tokens.Faint, 16), new LayoutParams(Ui.Dp(_ctx, 16), Ui.Dp(_ctx, 16)));
        Ui.MakeTappable(row, _openMods);

        if (_loadout.ChildCount > 0)
            _loadout.AddView(Ui.Divider(_ctx));
        _loadout.AddView(row);
    }

    void OnPlay()
    {
        if (_launching)
            return;

        if (!SMAPIInstaller.IsInstalled)
        {
            InstallSmapi();
            return;
        }

        _launching = true;
        Apply();
        Preferences.Set(LastPlayedKey, DateTime.UtcNow);
        LauncherActivity.Instance.OnClickStartGame();

        // if the game never opened (an error toast, say), don't leave the button stuck
        _play.PostDelayed(() =>
        {
            if (_launching)
                Refresh();
        }, 8000);
    }

    void InstallSmapi()
    {
        SMAPIInstaller.OnInstalledSMAPI = Refresh;
        SMAPIInstaller.OnClickInstallSMAPIZip(this, EventArgs.Empty);
    }

    void OnSmapiTap()
    {
        if (!SMAPIInstaller.IsInstalled)
        {
            InstallSmapi();
            return;
        }

        new AlertDialog.Builder(_ctx)
            .SetTitle($"SMAPI {SmapiVersion()}")
            .SetMessage("Built " + BuildDate(SMAPIInstaller.GetBuildCode()))
            .SetPositiveButton("Update from zip", (s, e) => InstallSmapi())
            .SetNegativeButton("Remove…", (s, e) => ConfirmRemove())
            .SetNeutralButton("Close", (s, e) => { })
            .Show();
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

    static string Greeting()
    {
        int hour = DateTime.Now.Hour;
        if (hour < 5) return "Up late";
        if (hour < 12) return "Good morning";
        if (hour < 18) return "Good afternoon";
        return "Good evening";
    }

    static string SmapiVersion()
    {
        try { return SMAPIInstaller.GetCurrentVersion().ToString(); }
        catch { return "unknown"; }
    }

    static string BuildDate(long unixSeconds)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("d MMM yyyy");
    }

    static string Ago(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} min ago";

        var local = utc.ToLocalTime();
        if (local.Date == DateTime.Now.Date) return "earlier today";
        if (local.Date == DateTime.Now.Date.AddDays(-1)) return "yesterday";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} days ago";
        return "on " + local.ToString("d MMM");
    }
}
