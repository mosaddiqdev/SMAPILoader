using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;

// Android.Content.Res and Android.Widget both define Orientation
using Orientation = Android.Widget.Orientation;

namespace SMAPIGameLoader.Launcher;

// Neutral charcoal. Text carries the hierarchy, colour is saved for one seasonal accent.
public static class Tokens
{
    public static readonly Color Bg = Color.ParseColor("#0F0F10");
    public static readonly Color Surface = Color.ParseColor("#151516");
    public static readonly Color Line = Color.ParseColor("#2A2A2C");
    public static readonly Color Divider = Color.ParseColor("#242426");
    public static readonly Color Text = Color.ParseColor("#F2F1EE");
    public static readonly Color TextSoft = Color.ParseColor("#D9D8D3");
    public static readonly Color Muted = Color.ParseColor("#9A9994");
    public static readonly Color Dim = Color.ParseColor("#8E8D89");
    public static readonly Color Faint = Color.ParseColor("#6F6E6A");
    public static readonly Color Danger = Color.ParseColor("#E0705A");

    // The game's own calendar: spring, summer, fall, winter. Real months stand in for in-game days.
    public static string SeasonName => DateTime.Now.Month switch
    {
        >= 3 and <= 5 => "Spring",
        >= 6 and <= 8 => "Summer",
        >= 9 and <= 11 => "Fall",
        _ => "Winter",
    };

    public static Color SeasonColor => SeasonName switch
    {
        "Spring" => Color.ParseColor("#8FB07A"),
        "Summer" => Color.ParseColor("#D4AE5A"),
        "Fall" => Color.ParseColor("#C98A52"),
        _ => Color.ParseColor("#8FA7B8"),
    };
}

internal static class Fonts
{
    static readonly Dictionary<Ui.Face, Typeface> Cache = new();

    public static Typeface Get(Context ctx, Ui.Face face)
    {
        if (Cache.TryGetValue(face, out var cached))
            return cached;

        Typeface font = face switch
        {
            Ui.Face.Medium => Typeface.Create("sans-serif-medium", TypefaceStyle.Normal),
            Ui.Face.Mono => Typeface.Monospace,
            Ui.Face.Display => Pixel(ctx, 500),
            Ui.Face.DisplayBold => Pixel(ctx, 620),
            _ => Typeface.Create("sans-serif", TypefaceStyle.Normal),
        };
        Cache[face] = font;
        return font;
    }

    // Pixelify Sans, bundled in Assets/fonts. Falls back to bold sans if the asset is missing.
    static Typeface Pixel(Context ctx, int weight)
    {
        try
        {
            var builder = new Typeface.Builder(ctx.Assets, "fonts/PixelifySans.ttf");
            builder.SetFontVariationSettings($"'wght' {weight}");
            return builder.Build() ?? Typeface.DefaultBold;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return Typeface.DefaultBold;
        }
    }
}

public static class Ui
{
    public static readonly int Match = ViewGroup.LayoutParams.MatchParent;
    public static readonly int Wrap = ViewGroup.LayoutParams.WrapContent;

    public enum Face { Regular, Medium, Mono, Display, DisplayBold }

    public static int Dp(Context context, float dp)
    {
        return (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, context.Resources.DisplayMetrics);
    }

    // half a dp, never thinner than one pixel
    static int Hair(Context ctx) => Math.Max(1, Dp(ctx, 0.5f));

    public static TextView Label(Context ctx, string text, float sp, Color color, Face face = Face.Regular)
    {
        var view = new TextView(ctx) { Text = text };
        view.SetTextSize(ComplexUnitType.Sp, sp);
        view.SetTextColor(color);
        view.SetIncludeFontPadding(false);
        view.Typeface = Fonts.Get(ctx, face);
        return view;
    }

    public static GradientDrawable Box(Context ctx, Color fill, float radiusDp, Color? stroke = null)
    {
        var box = new GradientDrawable();
        box.SetShape(ShapeType.Rectangle);
        box.SetColor(fill);
        box.SetCornerRadius(Dp(ctx, radiusDp));
        if (stroke is Color edge)
            box.SetStroke(Hair(ctx), edge);
        return box;
    }

    public static Drawable Ripple(Drawable content, Drawable mask, string tint = "#22FFFFFF")
    {
        return new RippleDrawable(ColorStateList.ValueOf(Color.ParseColor(tint)), content, mask);
    }

    public static void MakeTappable(View view, Action onTap)
    {
        view.Clickable = true;
        view.Focusable = true;
        view.Background = Ripple(null, new ColorDrawable(Color.White));
        view.Click += (_, _) => onTap();
    }

    public static View Divider(Context ctx)
    {
        var line = new View(ctx);
        line.SetBackgroundColor(Tokens.Divider);
        line.LayoutParameters = new LinearLayout.LayoutParams(Match, Hair(ctx));
        return line;
    }

    // A hairline-bordered group. Rows inside are clipped to its rounded corners.
    public static LinearLayout Panel(Context ctx)
    {
        var panel = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        panel.Background = Box(ctx, Tokens.Surface, 14, Tokens.Line);
        panel.ClipToOutline = true;
        panel.OutlineProvider = ViewOutlineProvider.Background;
        return panel;
    }

    public static Drawable Icon(Context ctx, string name, Color? tint = null)
    {
        int id = ctx.Resources.GetIdentifier(name, "drawable", ctx.PackageName);
        if (id == 0)
            return null;

        var drawable = ctx.GetDrawable(id)?.Mutate();
        if (drawable != null && tint is Color color)
            drawable.SetTint(color);
        return drawable;
    }

    public static ImageView IconView(Context ctx, string name, Color? tint, float sizeDp)
    {
        var view = new ImageView(ctx);
        view.SetImageDrawable(Icon(ctx, name, tint));
        view.LayoutParameters = new LinearLayout.LayoutParams(Dp(ctx, sizeDp), Dp(ctx, sizeDp));
        return view;
    }
}
