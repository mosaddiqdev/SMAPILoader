using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using System;

namespace SMAPIGameLoader.Launcher;

// The compatibility label and the popover that explains it.
// Compatible stays quiet, incompatible is the one loud thing in a row. No dots, no icons.
internal static class CompatUi
{
    public static void Apply(TextView badge, ModCompat compat)
    {
        bool ok = compat.Level == CompatLevel.Compatible;
        var ctx = badge.Context;

        badge.Text = ok ? "Compatible" : "Incompatible";
        badge.SetTextColor(ok ? Tokens.Ok : Tokens.Danger);
        badge.Gravity = GravityFlags.Center;

        if (ok)
        {
            badge.Background = null;
            badge.SetPadding(0, Ui.Dp(ctx, 2), Ui.Dp(ctx, 7), Ui.Dp(ctx, 2));
        }
        else
        {
            badge.Background = Ui.Box(ctx, Tokens.DangerWash, 6, Tokens.DangerEdge);
            badge.SetPadding(Ui.Dp(ctx, 7), Ui.Dp(ctx, 2), Ui.Dp(ctx, 7), Ui.Dp(ctx, 2));
        }
    }

    // The detail sheet shows the label as a full pill for both states.
    public static TextView Pill(Context ctx, ModCompat compat)
    {
        bool ok = compat.Level == CompatLevel.Compatible;
        var pill = Ui.Label(ctx, ok ? "Compatible" : "Incompatible", 12, ok ? Tokens.Ok : Tokens.Danger, Ui.Face.Medium);
        pill.Background = Ui.Box(ctx, ok ? Tokens.OkWash : Tokens.DangerWash, 7, ok ? Tokens.OkEdge : Tokens.DangerEdge);
        pill.SetPadding(Ui.Dp(ctx, 9), Ui.Dp(ctx, 3), Ui.Dp(ctx, 9), Ui.Dp(ctx, 3));
        return pill;
    }

    public static void ShowTip(View anchor, ModInfo mod)
    {
        var compat = mod.Compat;
        if (compat == null)
            return;

        var ctx = anchor.Context;
        bool ok = compat.Level == CompatLevel.Compatible;

        var box = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        box.Background = Ui.Box(ctx, Tokens.Elevated, 12, Tokens.LineStrong);
        box.SetPadding(Ui.Dp(ctx, 14), Ui.Dp(ctx, 12), Ui.Dp(ctx, 14), Ui.Dp(ctx, 12));

        string heading = ok
            ? "Compatible"
            : compat.Problems.Count == 1 ? "Won’t load" : $"{compat.Problems.Count} problems";
        box.AddView(Ui.Label(ctx, heading, 13, ok ? Tokens.Ok : Tokens.Danger, Ui.Face.Medium));

        var lines = ok ? compat.Passed : compat.Problems;
        for (int i = 0; i < lines.Count && i < 5; i++)
            box.AddView(Line(ctx, lines[i], ok ? Tokens.Muted : Tokens.TextSoft, 6, 13));
        if (lines.Count > 5)
            box.AddView(Line(ctx, $"and {lines.Count - 5} more", Tokens.Faint, 6, 13));

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

    static TextView Line(Context ctx, string text, Color color, int topDp, float sp)
    {
        var line = Ui.Label(ctx, text, sp, color);
        line.SetLineSpacing(0, 1.15f);
        line.LayoutParameters = new LinearLayout.LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, topDp) };
        return line;
    }
}
