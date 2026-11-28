using Android.Animation;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using System;

namespace SMAPIGameLoader.Launcher;

// The main button: a light pill with a silver ring, a bright rim and a soft inner shadow.
// It sinks a little when pressed, with a tick, and eases back when released.
internal sealed class Keycap : FrameLayout
{
    readonly FrameLayout _face;
    readonly TextView _label;
    readonly View _sweep;
    ObjectAnimator _sweepAnimator;

    public int TotalHeight { get; }

    public Keycap(Context ctx, float heightDp, float labelSp, Ui.Face face) : base(ctx)
    {
        TotalHeight = Ui.Dp(ctx, heightDp);
        Clickable = true;
        Focusable = true;

        // fully rounded, like the reference pill
        float density = ctx.Resources.DisplayMetrics.Density;
        _face = new FrameLayout(ctx);
        _face.Background = new BezelDrawable(BezelDrawable.Tone.Light, TotalHeight / 2f, density);
        _face.ClipToOutline = true;
        _face.OutlineProvider = ViewOutlineProvider.Background;
        AddView(_face, new LayoutParams(Ui.Match, Ui.Match));

        _label = Ui.Label(ctx, "", labelSp, Tokens.Bg, face);
        _label.Gravity = GravityFlags.Center;
        _label.SetPadding(Ui.Dp(ctx, 22), 0, Ui.Dp(ctx, 22), 0);
        _face.AddView(_label, new LayoutParams(Ui.Wrap, Ui.Wrap) { Gravity = GravityFlags.Center });

        // a dark line that travels along the inside of the pill while something is starting
        _sweep = new View(ctx);
        _sweep.SetBackgroundColor(Color.Argb(110, 15, 15, 16));
        _sweep.Visibility = ViewStates.Gone;
        _face.AddView(_sweep, new LayoutParams(Ui.Dp(ctx, 120), Ui.Dp(ctx, 2))
        {
            Gravity = GravityFlags.Bottom | GravityFlags.Start,
            BottomMargin = Ui.Dp(ctx, 7)
        });

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

    // The deeper inner shadow comes from the drawable's own pressed state. This adds the slight sink.
    void Press(bool down)
    {
        _face.Animate().Cancel();
        if (down)
        {
            _face.Animate().ScaleX(0.985f).ScaleY(0.985f).SetDuration(80).SetInterpolator(new DecelerateInterpolator()).Start();
        }
        else
        {
            _face.Animate().ScaleX(1f).ScaleY(1f).SetDuration(200).SetInterpolator(new OvershootInterpolator(2.2f)).Start();
        }
    }

    public void SetLabel(string text) => _label.Text = text;

    public void SetIcon(Drawable icon, float sizeDp = 16)
    {
        icon.SetBounds(0, 0, Ui.Dp(Context, sizeDp), Ui.Dp(Context, sizeDp));
        _label.SetCompoundDrawables(icon, null, null, null);
        _label.CompoundDrawablePadding = Ui.Dp(Context, 6);
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
