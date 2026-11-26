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

// Neutral charcoal. Text carries the hierarchy. Red is the only loud colour and only appears for problems.
public static class Tokens
{
    public static readonly Color Bg = Color.ParseColor("#0F0F10");
    public static readonly Color Surface = Color.ParseColor("#151516");
    public static readonly Color Elevated = Color.ParseColor("#1B1B1D");
    public static readonly Color SegmentOn = Color.ParseColor("#232325");
    public static readonly Color Line = Color.ParseColor("#2A2A2C");
    public static readonly Color LineStrong = Color.ParseColor("#343437");
    public static readonly Color Divider = Color.ParseColor("#242426");
    public static readonly Color Track = Color.ParseColor("#2A2A2C");
    public static readonly Color Text = Color.ParseColor("#F2F1EE");
    public static readonly Color TextSoft = Color.ParseColor("#D9D8D3");
    public static readonly Color Muted = Color.ParseColor("#9A9994");
    public static readonly Color Dim = Color.ParseColor("#8E8D89");
    public static readonly Color Faint = Color.ParseColor("#6F6E6A");
    public static readonly Color Ok = Color.ParseColor("#86A878");
    public static readonly Color OkWash = Color.Argb(28, 134, 168, 120);
    public static readonly Color OkEdge = Color.Argb(95, 134, 168, 120);
    public static readonly Color Danger = Color.ParseColor("#E0705A");
    public static readonly Color DangerWash = Color.Argb(33, 224, 112, 90);
    public static readonly Color DangerEdge = Color.Argb(115, 224, 112, 90);
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
    public enum Kind { Primary, Quiet, Danger }

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

    public static TextView Button(Context ctx, string text, Kind kind = Kind.Quiet, int heightDp = 44)
    {
        Color fill, ink;
        Color? edge = null;
        switch (kind)
        {
            case Kind.Primary:
                fill = Tokens.Text;
                ink = Tokens.Bg;
                break;
            case Kind.Danger:
                fill = Tokens.Surface;
                ink = Tokens.Danger;
                edge = Tokens.LineStrong;
                break;
            default:
                fill = Tokens.Surface;
                ink = Tokens.Text;
                edge = Tokens.LineStrong;
                break;
        }

        var button = Label(ctx, text, 14, ink, Face.Medium);
        button.Gravity = GravityFlags.Center;
        button.SetMinHeight(Dp(ctx, heightDp));
        button.SetPadding(Dp(ctx, 16), 0, Dp(ctx, 16), 0);
        button.Background = Ripple(Box(ctx, fill, 12, edge), Box(ctx, Color.White, 12),
            kind == Kind.Primary ? "#33000000" : "#22FFFFFF");
        button.Clickable = true;
        button.Focusable = true;
        return button;
    }

    // Small caps caption over a monospace value, for ids and paths.
    public static LinearLayout Field(Context ctx, string name, string value)
    {
        var field = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        var caption = Label(ctx, name.ToUpperInvariant(), 11, Tokens.Faint, Face.Mono);
        caption.LetterSpacing = 0.1f;
        var text = Label(ctx, value, 12.5f, Tokens.TextSoft, Face.Mono);
        text.SetTextIsSelectable(true);
        field.AddView(caption);
        field.AddView(text, new LinearLayout.LayoutParams(Match, Wrap) { TopMargin = Dp(ctx, 3) });
        return field;
    }

    public static LinearLayout SheetBody(Context ctx)
    {
        var body = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        body.SetPadding(Dp(ctx, 20), Dp(ctx, 4), Dp(ctx, 20), Dp(ctx, 24));
        return body;
    }
}
