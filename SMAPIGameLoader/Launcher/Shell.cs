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

// The floating bottom dock. Two things float side by side: a small pill of tabs, and the one primary key.
// The selected tab opens up to show its name, the others stay icons. The key changes with the page:
// Play on Home, Add mods on Mods. So the thumb always has the main action in the same place.
internal sealed class Dock : LinearLayout
{
    readonly Context _ctx;
    readonly LinearLayout _pill;
    readonly LinearLayout[] _tabs;
    readonly ImageView[] _icons;
    readonly TextView[] _labels;

    public Keycap Key { get; }
    public int Current { get; private set; } = -1;
    public event Action<int> Picked;

    public Dock(Context ctx, string[] names, string[] icons) : base(ctx)
    {
        _ctx = ctx;
        Orientation = Orientation.Horizontal;

        bool landscape = ctx.Resources.Configuration.Orientation == Android.Content.Res.Orientation.Landscape;
        int edge = Ui.Dp(ctx, landscape ? 24 : 16);
        SetPadding(edge, Ui.Dp(ctx, 8), edge, Ui.Dp(ctx, 10));
        SetGravity(landscape ? GravityFlags.Bottom | GravityFlags.CenterHorizontal : GravityFlags.Bottom);

        _pill = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
        _pill.SetGravity(GravityFlags.CenterVertical);
        _pill.Background = Ui.Box(ctx, Tokens.Surface, 21, Tokens.Line);
        _pill.SetPadding(Ui.Dp(ctx, 5), Ui.Dp(ctx, 5), Ui.Dp(ctx, 5), Ui.Dp(ctx, 5));

        _tabs = new LinearLayout[names.Length];
        _icons = new ImageView[names.Length];
        _labels = new TextView[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var tab = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
            tab.SetGravity(GravityFlags.CenterVertical);
            tab.SetPadding(Ui.Dp(ctx, 14), 0, Ui.Dp(ctx, 14), 0);
            tab.Clickable = true;
            tab.Focusable = true;
            tab.ContentDescription = names[i];
            tab.Click += (_, _) => Select(index);

            var icon = Ui.IconView(ctx, icons[i], Tokens.Faint, 22);
            var label = Ui.Label(ctx, names[i], 13, Tokens.Text, Ui.Face.Medium);
            label.Visibility = ViewStates.Gone;
            tab.AddView(icon);
            tab.AddView(label, new LayoutParams(Ui.Wrap, Ui.Wrap) { LeftMargin = Ui.Dp(ctx, 8) });

            _pill.AddView(tab, new LayoutParams(Ui.Wrap, Ui.Dp(ctx, 48)));
            _tabs[i] = tab;
            _icons[i] = icon;
            _labels[i] = label;
        }

        // the pill's bottom lines up with the key's face, not with the key's edge
        Key = new Keycap(ctx, 58, 18, 20, Ui.Face.DisplayBold);
        AddView(_pill, new LayoutParams(Ui.Wrap, Ui.Wrap) { BottomMargin = Ui.Dp(ctx, 3) });
        AddView(Key, landscape
            ? new LayoutParams(Ui.Dp(ctx, 220), Key.TotalHeight) { LeftMargin = Ui.Dp(ctx, 10) }
            : new LayoutParams(0, Key.TotalHeight, 1f) { LeftMargin = Ui.Dp(ctx, 10) });
    }

    GradientBackground TabBackground(bool selected)
    {
        return new GradientBackground(selected
            ? Ui.Ripple(Ui.Box(_ctx, Tokens.SegmentOn, 16, Tokens.LineStrong), Ui.Box(_ctx, Color.White, 16))
            : Ui.Ripple(null, Ui.Box(_ctx, Color.White, 16)));
    }

    // thin wrapper only so the two branches above share one return type
    readonly struct GradientBackground
    {
        public readonly Android.Graphics.Drawables.Drawable Value;
        public GradientBackground(Android.Graphics.Drawables.Drawable value) => Value = value;
    }

    public void Select(int index)
    {
        if (index == Current)
            return;

        // the chosen tab opens up and the old one closes, animated
        if (Current >= 0)
            Android.Transitions.TransitionManager.BeginDelayedTransition(_pill);

        Current = index;
        for (int i = 0; i < _tabs.Length; i++)
        {
            bool on = i == index;
            _icons[i].Drawable?.SetTint(on ? Tokens.Text : Tokens.Faint);
            _labels[i].Visibility = on ? ViewStates.Visible : ViewStates.Gone;
            _tabs[i].Background = TabBackground(on).Value;
        }
        Picked?.Invoke(index);
    }
}
