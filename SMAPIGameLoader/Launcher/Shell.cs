using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using System;

namespace SMAPIGameLoader.Launcher;

// Root of the launcher. Pads the page for the status bar, navigation bar and any side bars.
internal sealed class Shell : FrameLayout
{
    public LinearLayout Body { get; }

    public Shell(Context ctx) : base(ctx)
    {
        SetBackgroundColor(Tokens.Bg);
        Body = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        AddView(Body, new LayoutParams(Ui.Match, Ui.Match));
    }

    public override WindowInsets OnApplyWindowInsets(WindowInsets insets)
    {
        if (insets != null)
        {
            Body.SetPadding(
                insets.SystemWindowInsetLeft,
                insets.SystemWindowInsetTop,
                insets.SystemWindowInsetRight,
                insets.SystemWindowInsetBottom);
        }
        return base.OnApplyWindowInsets(insets);
    }
}

// Icon over label. Active is full white, inactive recedes. No pill, no indicator.
internal sealed class NavBar : LinearLayout
{
    readonly ImageView[] _icons;
    readonly TextView[] _labels;
    public event Action<int> Picked;

    public NavBar(Context ctx, string[] names, string[] icons) : base(ctx)
    {
        Orientation = Orientation.Vertical;
        SetBackgroundColor(Tokens.Bg);
        AddView(Ui.Divider(ctx));

        // landscape is short, so the bar gives some height back
        bool landscape = ctx.Resources.Configuration.Orientation == Android.Content.Res.Orientation.Landscape;
        int pad = Ui.Dp(ctx, landscape ? 6 : 10);

        var row = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
        AddView(row, new LayoutParams(Ui.Match, Ui.Wrap));

        _icons = new ImageView[names.Length];
        _labels = new TextView[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var item = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
            item.SetGravity(GravityFlags.CenterHorizontal);
            item.SetPadding(0, pad, 0, pad + Ui.Dp(ctx, 2));

            var icon = Ui.IconView(ctx, icons[i], Tokens.Faint, 22);
            var label = Ui.Label(ctx, names[i], 11, Tokens.Faint, Ui.Face.Medium);
            item.AddView(icon);
            item.AddView(label, new LayoutParams(Ui.Wrap, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 3) });

            Ui.MakeTappable(item, () => Select(index));
            row.AddView(item, new LayoutParams(0, Ui.Wrap, 1f));
            _icons[i] = icon;
            _labels[i] = label;
        }
    }

    public void Select(int index)
    {
        for (int i = 0; i < _icons.Length; i++)
        {
            var tint = i == index ? Tokens.Text : Tokens.Faint;
            _icons[i].Drawable?.SetTint(tint);
            _labels[i].SetTextColor(tint);
        }
        Picked?.Invoke(index);
    }
}
