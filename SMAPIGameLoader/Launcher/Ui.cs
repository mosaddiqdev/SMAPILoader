using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace SMAPIGameLoader.Launcher;

// One warm-charcoal palette. Gold is the only accent; green and red are reserved for status.
public static class Tokens
{
    public static readonly Color Bg = Color.ParseColor("#141413");
    public static readonly Color Surface = Color.ParseColor("#1B1B19");
    public static readonly Color Surface2 = Color.ParseColor("#222220");
    public static readonly Color Border = Color.ParseColor("#2E2D29");
    public static readonly Color Text = Color.ParseColor("#ECE7DA");
    public static readonly Color Muted = Color.ParseColor("#9C9687");
    public static readonly Color Faint = Color.ParseColor("#6F6B60");
    public static readonly Color Gold = Color.ParseColor("#E3A935");
    public static readonly Color GoldInk = Color.ParseColor("#1D1507");
    public static readonly Color Green = Color.ParseColor("#8BBF4C");
    public static readonly Color Red = Color.ParseColor("#D9644C");
}

public static class Ui
{
    public static readonly int Match = ViewGroup.LayoutParams.MatchParent;
    public static readonly int Wrap = ViewGroup.LayoutParams.WrapContent;

    public enum Kind { Primary, Quiet, Danger }

    public static int Dp(Context context, float dp)
    {
        return (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, context.Resources.DisplayMetrics);
    }

    public static TextView Label(Context ctx, string text, float sp, Color color, bool medium = false, bool mono = false)
    {
        var view = new TextView(ctx) { Text = text };
        view.SetTextSize(ComplexUnitType.Sp, sp);
        view.SetTextColor(color);
        view.IncludeFontPadding = false;
        view.Typeface = mono
            ? Typeface.Monospace
            : Typeface.Create(medium ? "sans-serif-medium" : "sans-serif", TypefaceStyle.Normal);
        return view;
    }

    public static GradientDrawable Box(Context ctx, Color fill, float radiusDp, Color? stroke = null)
    {
        var box = new GradientDrawable();
        box.SetShape(ShapeType.Rectangle);
        box.SetColor(fill);
        box.SetCornerRadius(Dp(ctx, radiusDp));
        if (stroke is Color edge)
            box.SetStroke(Dp(ctx, 1), edge);
        return box;
    }

    public static Drawable Ripple(Drawable content, Drawable mask)
    {
        var tint = ColorStateList.ValueOf(Color.ParseColor("#22FFFFFF"));
        return new RippleDrawable(tint, content, mask);
    }

    public static void MakeTappable(View view, Action onTap)
    {
        view.Clickable = true;
        view.Focusable = true;
        view.Background = Ripple(null, new ColorDrawable(Color.White));
        view.Click += (_, _) => onTap();
    }

    public static TextView Button(Context ctx, string text, Kind kind = Kind.Quiet)
    {
        Color fill, ink;
        Color? edge = null;
        switch (kind)
        {
            case Kind.Primary:
                fill = Tokens.Gold;
                ink = Tokens.GoldInk;
                break;
            case Kind.Danger:
                fill = Tokens.Surface;
                ink = Tokens.Red;
                edge = Tokens.Border;
                break;
            default:
                fill = Tokens.Surface2;
                ink = Tokens.Text;
                edge = Tokens.Border;
                break;
        }

        var button = Label(ctx, text, 15, ink, medium: true);
        button.Gravity = GravityFlags.Center;
        button.SetMinHeight(Dp(ctx, kind == Kind.Primary ? 52 : 44));
        button.SetPadding(Dp(ctx, 16), 0, Dp(ctx, 16), 0);
        button.Background = Ripple(Box(ctx, fill, 10, edge), Box(ctx, Color.White, 10));
        button.Clickable = true;
        button.Focusable = true;
        return button;
    }

    public static void SetAvailable(View view, bool available)
    {
        view.Enabled = available;
        view.Clickable = available;
        view.Alpha = available ? 1f : 0.4f;
    }

    public static View Dot(Context ctx, Color color, float sizeDp = 8)
    {
        var dot = new View(ctx);
        var shape = new GradientDrawable();
        shape.SetShape(ShapeType.Oval);
        shape.SetColor(color);
        dot.Background = shape;
        dot.LayoutParameters = new LinearLayout.LayoutParams(Dp(ctx, sizeDp), Dp(ctx, sizeDp));
        return dot;
    }

    public static void SetDotColor(View dot, Color color)
    {
        if (dot.Background is GradientDrawable shape)
            shape.SetColor(color);
    }

    public static View Divider(Context ctx)
    {
        var line = new View(ctx);
        line.SetBackgroundColor(Tokens.Border);
        line.LayoutParameters = new LinearLayout.LayoutParams(Match, Dp(ctx, 1));
        return line;
    }

    // A bordered, rounded surface. Rows inside it are clipped to the corners.
    public static LinearLayout Panel(Context ctx)
    {
        var panel = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        panel.Background = Box(ctx, Tokens.Surface, 12, Tokens.Border);
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

    public static LinearLayout SheetBody(Context ctx)
    {
        var body = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        body.SetPadding(Dp(ctx, 20), Dp(ctx, 12), Dp(ctx, 20), Dp(ctx, 24));
        return body;
    }

    public static LinearLayout Field(Context ctx, string name, string value, Color? valueColor = null)
    {
        var field = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        var caption = Label(ctx, name.ToUpperInvariant(), 11, Tokens.Faint, mono: true);
        caption.LetterSpacing = 0.1f;
        var text = Label(ctx, value, 13, valueColor ?? Tokens.Text, mono: true);
        text.SetTextIsSelectable(true);
        field.AddView(caption);
        field.AddView(text, new LinearLayout.LayoutParams(Match, Wrap) { TopMargin = Dp(ctx, 4) });
        return field;
    }
}

// Faint dither tiled behind everything. Drawn in code so there is no asset to ship.
public static class Pattern
{
    public static Drawable Create(Context ctx)
    {
        int pixel = Ui.Dp(ctx, 2);
        int cell = pixel * 12;

        var bitmap = Bitmap.CreateBitmap(cell, cell, Bitmap.Config.Argb8888);
        var canvas = new Canvas(bitmap);
        var paint = new Paint { Color = Color.Argb(11, 255, 240, 200) };
        canvas.DrawRect(0, 0, pixel, pixel, paint);
        canvas.DrawRect(cell / 2, cell / 2, cell / 2 + pixel, cell / 2 + pixel, paint);

        var tile = new BitmapDrawable(ctx.Resources, bitmap);
        tile.SetTileModeXY(Shader.TileMode.Repeat, Shader.TileMode.Repeat);
        return new LayerDrawable(new Drawable[] { new ColorDrawable(Tokens.Bg), tile });
    }
}
