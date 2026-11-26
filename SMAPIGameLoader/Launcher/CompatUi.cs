using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace SMAPIGameLoader.Launcher;

// The compatibility label. Compatible stays quiet, incompatible is the one loud thing in a row.
// No dots, no icons. The reasons live in the mod's drawer.
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

    // The drawer shows the label as a full pill for both states.
    public static TextView Pill(Context ctx, ModCompat compat)
    {
        bool ok = compat.Level == CompatLevel.Compatible;
        var pill = Ui.Label(ctx, ok ? "Compatible" : "Incompatible", 12, ok ? Tokens.Ok : Tokens.Danger, Ui.Face.Medium);
        pill.Background = Ui.Box(ctx, ok ? Tokens.OkWash : Tokens.DangerWash, 7, ok ? Tokens.OkEdge : Tokens.DangerEdge);
        pill.SetPadding(Ui.Dp(ctx, 9), Ui.Dp(ctx, 3), Ui.Dp(ctx, 9), Ui.Dp(ctx, 3));
        return pill;
    }
}
