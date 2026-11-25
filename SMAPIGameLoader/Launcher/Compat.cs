using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SMAPIGameLoader.Launcher;

public enum CompatLevel { Compatible, Incompatible }

// What we could verify about a mod from its manifest and your setup.
public sealed class ModCompat
{
    public CompatLevel Level { get; init; }
    public List<string> Problems { get; init; } = new();
    public List<string> Passed { get; init; } = new();
}

// Manifest versions look like "4.0.0", "1.6.15" or "2.1.0-beta.3". Only the numbers matter here.
public static class VersionText
{
    static readonly Regex Numbers = new(@"^\s*v?(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:\.(\d+))?", RegexOptions.Compiled);

    public static Version Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = Numbers.Match(text);
        if (!match.Success)
            return null;

        int Part(int index) => match.Groups[index].Success ? int.Parse(match.Groups[index].Value) : 0;
        return new Version(Part(1), Part(2), Part(3), Part(4));
    }
}

// The badge and the popover that explains it. No dots or icons, the words do the work.
internal static class CompatUi
{
    static readonly Color GreenWash = Color.Argb(34, 139, 191, 76);
    static readonly Color GreenEdge = Color.Argb(90, 139, 191, 76);
    static readonly Color RedWash = Color.Argb(34, 217, 100, 76);
    static readonly Color RedEdge = Color.Argb(100, 217, 100, 76);

    public static void Apply(TextView badge, ModCompat compat)
    {
        bool ok = compat.Level == CompatLevel.Compatible;
        var ctx = badge.Context;

        badge.Text = ok ? "Compatible" : "Incompatible";
        badge.SetTextColor(ok ? Tokens.Green : Tokens.Red);
        badge.Background = Ui.Box(ctx, ok ? GreenWash : RedWash, 6, ok ? GreenEdge : RedEdge);
        badge.Gravity = GravityFlags.Center;
        badge.SetPadding(Ui.Dp(ctx, 8), Ui.Dp(ctx, 3), Ui.Dp(ctx, 8), Ui.Dp(ctx, 3));
    }

    public static TextView Badge(Context ctx, ModCompat compat)
    {
        var badge = Ui.Label(ctx, "", 11, Tokens.Text, medium: true);
        Apply(badge, compat);
        return badge;
    }

    public static void ShowTip(View anchor, ModInfo mod)
    {
        var compat = mod.Compat;
        if (compat == null)
            return;

        var ctx = anchor.Context;
        bool ok = compat.Level == CompatLevel.Compatible;

        var box = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        box.Background = Ui.Box(ctx, Tokens.Surface2, 10, Tokens.Border);
        box.SetPadding(Ui.Dp(ctx, 14), Ui.Dp(ctx, 12), Ui.Dp(ctx, 14), Ui.Dp(ctx, 12));

        string heading = ok
            ? "Compatible"
            : compat.Problems.Count == 1 ? "Won’t load" : $"{compat.Problems.Count} problems";
        box.AddView(Ui.Label(ctx, heading, 13, ok ? Tokens.Green : Tokens.Red, medium: true));

        var lines = ok ? compat.Passed : compat.Problems;
        for (int i = 0; i < lines.Count && i < 5; i++)
            box.AddView(Line(ctx, lines[i], ok ? Tokens.Muted : Tokens.Text, 6));
        if (lines.Count > 5)
            box.AddView(Line(ctx, $"and {lines.Count - 5} more", Tokens.Faint, 6));

        string note = null;
        if (ok)
            note = "Checked against the mod’s manifest. Whether its code runs on Android can’t be checked here.";
        else if (!mod.Enabled)
            note = "This mod is turned off, so it can’t cause trouble until you turn it on.";
        if (note != null)
            box.AddView(Line(ctx, note, Tokens.Faint, 10, 12));

        int width = Math.Min(Ui.Dp(ctx, 280), ctx.Resources.DisplayMetrics.WidthPixels - Ui.Dp(ctx, 32));
        var popup = new PopupWindow(box, width, Ui.Wrap, true);
        popup.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
        popup.OutsideTouchable = true;
        popup.Elevation = Ui.Dp(ctx, 8);
        popup.ShowAsDropDown(anchor, 0, Ui.Dp(ctx, 6));
    }

    static TextView Line(Context ctx, string text, Color color, int topDp, float sp = 13)
    {
        var line = Ui.Label(ctx, text, sp, color);
        line.SetLineSpacing(0, 1.15f);
        line.LayoutParameters = new LinearLayout.LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, topDp) };
        return line;
    }
}
