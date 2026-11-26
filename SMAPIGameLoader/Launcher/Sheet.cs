using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using System;

namespace SMAPIGameLoader.Launcher;

// Bottom sheet drawn inside the activity. Tall content scrolls, and on wide screens it stays a comfortable width.
internal sealed class SheetHost : FrameLayout
{
    readonly View _scrim;
    readonly LinearLayout _panel;
    readonly ScrollView _scroll;
    public bool IsOpen { get; private set; }

    public SheetHost(Context ctx) : base(ctx)
    {
        Visibility = ViewStates.Gone;

        _scrim = new View(ctx);
        _scrim.SetBackgroundColor(Color.ParseColor("#A6000000"));
        _scrim.Click += (_, _) => Hide();
        AddView(_scrim, new LayoutParams(Ui.Match, Ui.Match));

        float radius = Ui.Dp(ctx, 20);
        var shape = new GradientDrawable();
        shape.SetColor(Tokens.Elevated);
        shape.SetStroke(Math.Max(1, Ui.Dp(ctx, 0.5f)), Tokens.LineStrong);
        shape.SetCornerRadii(new float[] { radius, radius, radius, radius, 0, 0, 0, 0 });

        int width = Math.Min(ctx.Resources.DisplayMetrics.WidthPixels, Ui.Dp(ctx, 560));
        _panel = new LinearLayout(ctx) { Orientation = Orientation.Vertical, Clickable = true };
        _panel.Background = shape;

        var handle = new View(ctx);
        handle.Background = Ui.Box(ctx, Color.ParseColor("#3A3A3D"), 2);
        _panel.AddView(handle, new LinearLayout.LayoutParams(Ui.Dp(ctx, 36), Ui.Dp(ctx, 4))
        {
            Gravity = GravityFlags.CenterHorizontal,
            TopMargin = Ui.Dp(ctx, 10),
            BottomMargin = Ui.Dp(ctx, 14)
        });

        _scroll = new ScrollView(ctx) { VerticalScrollBarEnabled = false };
        _panel.AddView(_scroll, new LinearLayout.LayoutParams(Ui.Match, Ui.Wrap));

        AddView(_panel, new LayoutParams(width, Ui.Wrap)
        {
            Gravity = GravityFlags.Bottom | GravityFlags.CenterHorizontal,
            TopMargin = Ui.Dp(ctx, 48)
        });
    }

    public override WindowInsets OnApplyWindowInsets(WindowInsets insets)
    {
        if (insets != null)
            _panel.SetPadding(0, 0, 0, insets.SystemWindowInsetBottom);
        return base.OnApplyWindowInsets(insets);
    }

    public void Show(View content)
    {
        _scroll.RemoveAllViews();
        _scroll.AddView(content);
        _scroll.ScrollTo(0, 0);
        IsOpen = true;
        Visibility = ViewStates.Visible;

        _scrim.Alpha = 0f;
        _panel.TranslationY = Resources.DisplayMetrics.HeightPixels;
        _panel.Post(() =>
        {
            _scrim.Animate().Alpha(1f).SetDuration(160).Start();
            _panel.Animate().TranslationY(0f).SetDuration(220).SetInterpolator(new DecelerateInterpolator()).Start();
        });
    }

    public void Hide()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        _scrim.Animate().Alpha(0f).SetDuration(120).Start();
        _panel.Animate()
            .TranslationY(_panel.Height)
            .SetDuration(160)
            .WithEndAction(new Java.Lang.Runnable(() =>
            {
                if (!IsOpen)
                    Visibility = ViewStates.Gone;
            }))
            .Start();
    }
}
