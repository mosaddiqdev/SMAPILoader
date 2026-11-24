using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using System;

namespace SMAPIGameLoader.Launcher;

// Root of the launcher. Pads the page for the status and navigation bars.
internal sealed class Shell : FrameLayout
{
    public LinearLayout Body { get; }

    public Shell(Context ctx) : base(ctx)
    {
        Background = Pattern.Create(ctx);
        Body = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        AddView(Body, new LayoutParams(Ui.Match, Ui.Match));
    }

    public override WindowInsets OnApplyWindowInsets(WindowInsets insets)
    {
        if (insets != null)
            Body.SetPadding(0, insets.SystemWindowInsetTop, 0, insets.SystemWindowInsetBottom);
        return base.OnApplyWindowInsets(insets);
    }
}

internal sealed class TabBar : LinearLayout
{
    readonly TextView[] _labels;
    readonly View[] _lines;
    public event Action<int> Selected;

    public TabBar(Context ctx, params string[] names) : base(ctx)
    {
        Orientation = Orientation.Vertical;
        SetBackgroundColor(Tokens.Surface);
        AddView(Ui.Divider(ctx));

        var row = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
        AddView(row, new LayoutParams(Ui.Match, Ui.Wrap));

        _labels = new TextView[names.Length];
        _lines = new View[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var cell = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
            var line = new View(ctx);
            var label = Ui.Label(ctx, names[i], 14, Tokens.Muted, medium: true);
            label.Gravity = GravityFlags.Center;

            cell.AddView(line, new LayoutParams(Ui.Match, Ui.Dp(ctx, 2)));
            cell.AddView(label, new LayoutParams(Ui.Match, Ui.Dp(ctx, 50)));
            Ui.MakeTappable(cell, () => Select(index));
            row.AddView(cell, new LayoutParams(0, Ui.Wrap, 1f));

            _labels[i] = label;
            _lines[i] = line;
        }
    }

    public void Select(int index)
    {
        for (int i = 0; i < _labels.Length; i++)
        {
            bool active = i == index;
            _labels[i].SetTextColor(active ? Tokens.Text : Tokens.Muted);
            _lines[i].SetBackgroundColor(active ? Tokens.Gold : Color.Transparent);
        }
        Selected?.Invoke(index);
    }
}

// Bottom sheet drawn inside the activity, so we control the look end to end.
internal sealed class SheetHost : FrameLayout
{
    readonly View _scrim;
    readonly FrameLayout _panel;
    public bool IsOpen { get; private set; }

    public SheetHost(Context ctx) : base(ctx)
    {
        Visibility = ViewStates.Gone;

        _scrim = new View(ctx);
        _scrim.SetBackgroundColor(Color.ParseColor("#B3000000"));
        _scrim.Click += (_, _) => Hide();
        AddView(_scrim, new LayoutParams(Ui.Match, Ui.Match));

        float r = Ui.Dp(ctx, 16);
        var shape = new GradientDrawable();
        shape.SetColor(Tokens.Surface2);
        shape.SetStroke(Ui.Dp(ctx, 1), Tokens.Border);
        shape.SetCornerRadii(new float[] { r, r, r, r, 0, 0, 0, 0 });

        _panel = new FrameLayout(ctx) { Clickable = true };
        _panel.Background = shape;
        AddView(_panel, new LayoutParams(Ui.Match, Ui.Wrap) { Gravity = GravityFlags.Bottom });
    }

    public override WindowInsets OnApplyWindowInsets(WindowInsets insets)
    {
        if (insets != null)
            _panel.SetPadding(0, 0, 0, insets.SystemWindowInsetBottom);
        return base.OnApplyWindowInsets(insets);
    }

    public void Show(View content)
    {
        _panel.RemoveAllViews();
        _panel.AddView(content);
        IsOpen = true;
        Visibility = ViewStates.Visible;

        _scrim.Alpha = 0f;
        _panel.TranslationY = Resources.DisplayMetrics.HeightPixels;
        _panel.Post(() =>
        {
            _scrim.Animate().Alpha(1f).SetDuration(150).Start();
            _panel.Animate().TranslationY(0f).SetDuration(200).SetInterpolator(new DecelerateInterpolator()).Start();
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
            .SetDuration(150)
            .WithEndAction(new Java.Lang.Runnable(() =>
            {
                if (!IsOpen)
                    Visibility = ViewStates.Gone;
            }))
            .Start();
    }
}
