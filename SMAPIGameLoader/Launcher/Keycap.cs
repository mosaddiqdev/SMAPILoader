using Android.Animation;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using System;

namespace SMAPIGameLoader.Launcher;

// A tactile key: a lit face resting on a darker edge. It presses down on touch and springs back on release.
// Used for the few buttons that matter most, so they feel like the thing to press.
internal sealed class Keycap : FrameLayout
{
    readonly FrameLayout _face;
    readonly TextView _label;
    readonly View _sweep;
    readonly int _lift;
    ObjectAnimator _sweepAnimator;

    // height the caller should give this view: the face plus the visible edge
    public int TotalHeight { get; }

    public Keycap(Context ctx, float heightDp, float radiusDp, float labelSp, Ui.Face face) : base(ctx)
    {
        _lift = Ui.Dp(ctx, 3);
        TotalHeight = Ui.Dp(ctx, heightDp) + _lift;
        Clickable = true;
        Focusable = true;

        // the edge the face rests on
        var edge = new View(ctx);
        edge.Background = Ui.Box(ctx, Color.ParseColor("#A3A29C"), radiusDp);
        AddView(edge, new LayoutParams(Ui.Match, Ui.Match));

        // the face: soft light from top to bottom, with a faint bright rim on top of it
        var fill = new GradientDrawable(GradientDrawable.Orientation.TopBottom,
            new[] { Color.ParseColor("#FAF9F6").ToArgb(), Color.ParseColor("#E7E6E1").ToArgb() });
        fill.SetCornerRadius(Ui.Dp(ctx, radiusDp));

        _face = new FrameLayout(ctx);
        _face.Background = fill;
        _face.Foreground = Ui.Ripple(null, Ui.Box(ctx, Color.White, radiusDp), "#26000000");
        _face.ClipToOutline = true;
        _face.OutlineProvider = ViewOutlineProvider.Background;
        AddView(_face, new LayoutParams(Ui.Match, Ui.Match) { BottomMargin = _lift });

        var rim = new View(ctx);
        rim.Background = Ui.Box(ctx, Color.Transparent, radiusDp, Color.Argb(150, 255, 255, 255));
        _face.AddView(rim, new LayoutParams(Ui.Match, Ui.Match));

        _label = Ui.Label(ctx, "", labelSp, Tokens.Bg, face);
        _label.Gravity = GravityFlags.Center;
        _label.SetPadding(Ui.Dp(ctx, 16), 0, Ui.Dp(ctx, 16), 0);
        _face.AddView(_label, new LayoutParams(Ui.Wrap, Ui.Wrap) { Gravity = GravityFlags.Center });

        // a dark line that travels across the face while something is starting
        _sweep = new View(ctx);
        _sweep.SetBackgroundColor(Color.Argb(110, 15, 15, 16));
        _sweep.Visibility = ViewStates.Gone;
        _face.AddView(_sweep, new LayoutParams(Ui.Dp(ctx, 120), Ui.Dp(ctx, 2)) { Gravity = GravityFlags.Bottom | GravityFlags.Start });

        Touch += (_, e) =>
        {
            switch (e.Event.ActionMasked)
            {
                case MotionEventActions.Down:
                    PerformHapticFeedback(FeedbackConstants.VirtualKey);
                    Press(true);
                    break;
                case MotionEventActions.Up:
                    PerformHapticFeedback(FeedbackConstants.VirtualKeyRelease);
                    Press(false);
                    break;
                case MotionEventActions.Cancel:
                    Press(false);
                    break;
            }
            // never swallow the touch, the click still has to fire
            e.Handled = false;
        };
    }

    void Press(bool down)
    {
        _face.Animate().Cancel();
        if (down)
        {
            _face.Animate().TranslationY(_lift).SetDuration(70).SetInterpolator(new DecelerateInterpolator()).Start();
        }
        else
        {
            _face.Animate().TranslationY(0f).SetDuration(190).SetInterpolator(new OvershootInterpolator(2.4f)).Start();
        }
    }

    public void SetLabel(string text) => _label.Text = text;

    public void SetIcon(Drawable icon, float sizeDp = 16)
    {
        icon.SetBounds(0, 0, Ui.Dp(Context, sizeDp), Ui.Dp(Context, sizeDp));
        _label.SetCompoundDrawables(icon, null, null, null);
        _label.CompoundDrawablePadding = Ui.Dp(Context, 6);
        _label.SetPadding(Ui.Dp(Context, 12), 0, Ui.Dp(Context, 16), 0);
    }

    public void Set(string text, bool busy)
    {
        _label.Text = text;
        if (busy)
            StartSweep();
        else
            StopSweep();
    }

    void StartSweep()
    {
        if (_sweepAnimator != null)
            return;

        _sweep.Visibility = ViewStates.Visible;
        float travel = Resources.DisplayMetrics.WidthPixels;
        _sweepAnimator = ObjectAnimator.OfFloat(_sweep, "translationX", -travel * 0.4f, travel);
        _sweepAnimator.SetDuration(1100);
        _sweepAnimator.RepeatCount = ValueAnimator.Infinite;
        _sweepAnimator.Start();
    }

    void StopSweep()
    {
        _sweepAnimator?.Cancel();
        _sweepAnimator = null;
        _sweep.TranslationX = 0;
        _sweep.Visibility = ViewStates.Gone;
    }
}
