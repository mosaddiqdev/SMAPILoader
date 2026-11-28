using Android.Graphics;
using Android.Graphics.Drawables;
using System;
using System.Collections.Generic;

namespace SMAPIGameLoader.Launcher;

// The pill look: a thin outer ring, a bright rim just inside it, a body lit from the top,
// and a soft inner shadow that sinks the face. Painted once per size and state, so it costs
// nothing while scrolling or animating. Pressed swaps to a deeper shadow, so any view that
// wears this drawable gets its pressed look for free.
internal sealed class BezelDrawable : Drawable
{
    public enum Tone { Light, Dark }

    readonly Tone _tone;
    readonly float _radius;
    readonly float _density;
    readonly Paint _paint = new Paint(PaintFlags.FilterBitmap);
    readonly Dictionary<bool, Bitmap> _cache = new();
    bool _pressed;
    int _width;
    int _height;

    public BezelDrawable(Tone tone, float radiusPx, float density)
    {
        _tone = tone;
        _radius = radiusPx;
        _density = density;
    }

    public override int Opacity => (int)Format.Translucent;

    public override bool IsStateful => true;

    public override void SetAlpha(int alpha)
    {
        _paint.Alpha = alpha;
        InvalidateSelf();
    }

    public override void SetColorFilter(ColorFilter colorFilter)
    {
        _paint.SetColorFilter(colorFilter);
        InvalidateSelf();
    }

    protected override bool OnStateChange(int[] state)
    {
        bool pressed = state != null && Array.IndexOf(state, Android.Resource.Attribute.StatePressed) >= 0;
        if (pressed == _pressed)
            return false;

        _pressed = pressed;
        InvalidateSelf();
        return true;
    }

    // lets a view clip its children (a progress line, say) to the pill
    public override void GetOutline(Outline outline)
    {
        var bounds = Bounds;
        float radius = Math.Min(_radius, Math.Min(bounds.Width(), bounds.Height()) / 2f);
        outline.SetRoundRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom, radius);
    }

    public override void Draw(Canvas canvas)
    {
        var bounds = Bounds;
        int width = bounds.Width();
        int height = bounds.Height();
        if (width <= 0 || height <= 0)
            return;

        if (width != _width || height != _height)
        {
            foreach (var old in _cache.Values)
                old.Recycle();
            _cache.Clear();
            _width = width;
            _height = height;
        }

        if (!_cache.TryGetValue(_pressed, out var bitmap))
        {
            bitmap = Render(width, height, _pressed);
            _cache[_pressed] = bitmap;
        }
        canvas.DrawBitmap(bitmap, null, bounds, _paint);
    }

    static LinearGradient Vertical(float height, float[] stops, params string[] colors)
    {
        var argb = new int[colors.Length];
        for (int i = 0; i < colors.Length; i++)
            argb[i] = Color.ParseColor(colors[i]).ToArgb();
        return new LinearGradient(0f, 0f, 0f, height, argb, stops, Shader.TileMode.Clamp);
    }

    Bitmap Render(int width, int height, bool pressed)
    {
        var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888);
        var canvas = new Canvas(bitmap);

        bool light = _tone == Tone.Light;
        float radius = Math.Min(_radius, Math.Min(width, height) / 2f);
        float ring = Math.Max(1f, _density * 1.5f);
        float hair = Math.Max(1f, _density * 0.75f);
        var ends = new[] { 0f, 1f };
        var three = new[] { 0f, 0.55f, 1f };

        // outer ring: silver on light, near black on dark
        var ringPaint = new Paint(PaintFlags.AntiAlias);
        ringPaint.SetShader(light ? Vertical(height, ends, "#DAD9D4", "#A9A8A2") : Vertical(height, ends, "#000000", "#000000"));
        canvas.DrawRoundRect(new RectF(0f, 0f, width, height), radius, radius, ringPaint);

        // the body
        var body = new RectF(ring, ring, width - ring, height - ring);
        float bodyRadius = Math.Max(0f, radius - ring);
        var bodyPaint = new Paint(PaintFlags.AntiAlias);
        if (light)
        {
            bodyPaint.SetShader(pressed
                ? Vertical(height, ends, "#EEEDE9", "#DEDDD8")
                : Vertical(height, ends, "#FAF9F6", "#E8E7E2"));
        }
        else
        {
            bodyPaint.SetShader(pressed
                ? Vertical(height, three, "#2C2C2E", "#141415", "#030303")
                : Vertical(height, three, "#3A3A3C", "#1C1C1E", "#050505"));
        }
        canvas.DrawRoundRect(body, bodyRadius, bodyRadius, bodyPaint);

        // inner shadow: blur everything outside the body, but only let it show inside the body
        var bodyPath = new Path();
        bodyPath.AddRoundRect(body, bodyRadius, bodyRadius, Path.Direction.Cw);
        var outside = new Path();
        outside.AddRoundRect(body, bodyRadius, bodyRadius, Path.Direction.Cw);
        outside.SetFillType(Path.FillType.InverseEvenOdd);

        int shadowAlpha = light ? (pressed ? 95 : 48) : (pressed ? 190 : 150);
        var shadow = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(shadowAlpha, 0, 0, 0) };
        shadow.SetMaskFilter(new BlurMaskFilter(Math.Max(2f, height * 0.16f), BlurMaskFilter.Blur.Normal));

        canvas.Save();
        canvas.ClipPath(bodyPath);
        // resting, the shadow gathers a little more at the bottom. pressed, it falls from the top.
        canvas.Translate(0f, pressed ? height * 0.05f : -height * 0.03f);
        canvas.DrawPath(outside, shadow);
        canvas.Restore();

        // the bright rim just inside the ring, brighter along the top
        var rim = new Paint(PaintFlags.AntiAlias);
        rim.SetStyle(Paint.Style.Stroke);
        rim.StrokeWidth = hair;
        rim.SetShader(light
            ? Vertical(height, ends, "#F2FFFFFF", "#33FFFFFF")
            : Vertical(height, ends, "#52FFFFFF", "#1FFFFFFF"));
        var rimRect = new RectF(body);
        rimRect.Inset(hair / 2f, hair / 2f);
        float rimRadius = Math.Max(0f, bodyRadius - hair / 2f);
        canvas.DrawRoundRect(rimRect, rimRadius, rimRadius, rim);

        return bitmap;
    }
}
