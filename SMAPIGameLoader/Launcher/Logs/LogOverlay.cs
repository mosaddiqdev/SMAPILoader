using Android.App;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;
using Android.Widget;
using System;
using Xamarin.Essentials;

namespace SMAPIGameLoader.Launcher;

// A small draggable button inside the game. Tap it to read SMAPI's log without leaving the game.
// It lives in the game activity's window, so it needs no "draw over other apps" permission.
internal sealed class LogOverlay : Java.Lang.Object, Application.IActivityLifecycleCallbacks
{
    public const string PrefKey = "log_button";
    public static bool ButtonEnabled => Preferences.Get(PrefKey, true);

    static LogOverlay _instance;

    public static void EnsureRegistered(Application app)
    {
        if (_instance != null || app == null)
            return;

        _instance = new LogOverlay();
        app.RegisterActivityLifecycleCallbacks(_instance);
    }

    readonly Handler _handler = new Handler(Looper.MainLooper);
    readonly Java.Lang.Runnable _tick;
    LogSession _session;
    Activity _host;
    ViewGroup _decor;
    FrameLayout _fab;
    View _badge;
    LogView _panel;
    bool _polling;
    bool _dragging;
    float _downX, _downY, _startX, _startY;

    LogOverlay()
    {
        _tick = new Java.Lang.Runnable(Tick);
    }

    public void OnActivityCreated(Activity activity, Bundle savedInstanceState)
    {
        if (activity is not SMAPIActivity)
            return;

        // Only the window must wait: touching it this early would break the game's own window setup.
        _session = new LogSession(DateTime.UtcNow.AddSeconds(-3));
        StartPolling();
    }

    public void OnActivityResumed(Activity activity)
    {
        if (activity is not SMAPIActivity)
            return;

        _session ??= new LogSession(DateTime.UtcNow.AddSeconds(-3));
        if (ButtonEnabled)
            Attach(activity);
        StartPolling();
    }

    public void OnActivityPaused(Activity activity)
    {
        if (activity is SMAPIActivity)
            StopPolling();
    }

    public void OnActivityDestroyed(Activity activity)
    {
        if (activity is not SMAPIActivity)
            return;

        StopPolling();
        Detach();
        _session = null;
    }

    public void OnActivityStarted(Activity activity) { }
    public void OnActivityStopped(Activity activity) { }
    public void OnActivitySaveInstanceState(Activity activity, Bundle outState) { }

    void StartPolling()
    {
        StopPolling();
        _polling = true;
        _handler.Post(_tick);
    }

    void StopPolling()
    {
        _polling = false;
        _handler.RemoveCallbacks(_tick);
    }

    void Tick()
    {
        if (!_polling)
            return;

        try
        {
            if (_session != null && _session.Poll())
            {
                UpdateBadge();
                _panel?.Refresh();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        _handler.PostDelayed(_tick, 700);
    }

    void UpdateBadge()
    {
        if (_badge == null || _session == null)
            return;

        if (_session.Errors > 0)
        {
            Ui.SetDotColor(_badge, Tokens.Red);
            _badge.Visibility = ViewStates.Visible;
        }
        else if (_session.Warnings > 0)
        {
            Ui.SetDotColor(_badge, Tokens.Gold);
            _badge.Visibility = ViewStates.Visible;
        }
        else
        {
            _badge.Visibility = ViewStates.Gone;
        }
    }

    void Attach(Activity activity)
    {
        if (_host == activity && _fab != null)
            return;

        Detach();
        _host = activity;
        _decor = (ViewGroup)activity.Window.DecorView;
        int size = Ui.Dp(activity, 44);

        var disc = new GradientDrawable();
        disc.SetShape(ShapeType.Oval);
        disc.SetColor(Color.ParseColor("#D9141413"));
        disc.SetStroke(Ui.Dp(activity, 1), Tokens.Border);

        _fab = new FrameLayout(activity) { Alpha = 0.7f, Clickable = true, ContentDescription = "SMAPI log" };
        _fab.Background = disc;
        _fab.AddView(Ui.IconView(activity, "ic_terminal", Tokens.Gold, 22),
            new FrameLayout.LayoutParams(Ui.Dp(activity, 22), Ui.Dp(activity, 22)) { Gravity = GravityFlags.Center });

        _badge = Ui.Dot(activity, Tokens.Red, 10);
        _badge.Visibility = ViewStates.Gone;
        _fab.AddView(_badge, new FrameLayout.LayoutParams(Ui.Dp(activity, 10), Ui.Dp(activity, 10))
        {
            Gravity = GravityFlags.Top | GravityFlags.End,
            TopMargin = Ui.Dp(activity, 6),
            RightMargin = Ui.Dp(activity, 6)
        });
        _fab.Touch += OnFabTouch;

        _decor.AddView(_fab, new FrameLayout.LayoutParams(size, size)
        {
            Gravity = GravityFlags.Top | GravityFlags.Start,
            LeftMargin = Ui.Dp(activity, 10),
            TopMargin = Ui.Dp(activity, 90)
        });
        UpdateBadge();
    }

    void Detach()
    {
        if (_decor != null)
        {
            if (_panel != null) _decor.RemoveView(_panel);
            if (_fab != null) _decor.RemoveView(_fab);
        }
        _panel = null;
        _fab = null;
        _badge = null;
        _decor = null;
        _host = null;
    }

    void OnFabTouch(object sender, View.TouchEventArgs e)
    {
        var ev = e.Event;
        switch (ev.ActionMasked)
        {
            case MotionEventActions.Down:
                _downX = ev.RawX;
                _downY = ev.RawY;
                _startX = _fab.TranslationX;
                _startY = _fab.TranslationY;
                _dragging = false;
                _fab.Alpha = 1f;
                break;

            case MotionEventActions.Move:
                {
                    float dx = ev.RawX - _downX;
                    float dy = ev.RawY - _downY;
                    float slop = ViewConfiguration.Get(_host).ScaledTouchSlop;
                    if (!_dragging && (Math.Abs(dx) > slop || Math.Abs(dy) > slop))
                        _dragging = true;
                    if (_dragging)
                    {
                        _fab.TranslationX = _startX + dx;
                        _fab.TranslationY = _startY + dy;
                    }
                    break;
                }

            case MotionEventActions.Up:
                if (_dragging)
                    SnapToEdge();
                else
                    Toggle();
                _fab.Alpha = 0.7f;
                break;

            case MotionEventActions.Cancel:
                _fab.Alpha = 0.7f;
                break;
        }
        e.Handled = true;
    }

    // After a drag the button settles on the nearest side so it never sits in the middle of the game.
    void SnapToEdge()
    {
        int pad = Ui.Dp(_host, 8);
        float left = _fab.Left + _fab.TranslationX;
        float top = _fab.Top + _fab.TranslationY;
        float targetLeft = left + _fab.Width / 2f < _decor.Width / 2f ? pad : _decor.Width - _fab.Width - pad;
        float targetTop = Math.Clamp(top, pad, _decor.Height - _fab.Height - pad);
        _fab.Animate()
            .TranslationX(targetLeft - _fab.Left)
            .TranslationY(targetTop - _fab.Top)
            .SetDuration(150)
            .Start();
    }

    void Toggle()
    {
        if (_panel == null)
            Open();
        else
            Close();
    }

    void Open()
    {
        if (_panel != null || _host == null || _session == null)
            return;

        int height = (int)(_decor.Height * 0.62f);
        int width = Math.Min(_decor.Width, Ui.Dp(_host, 760));

        _panel = new LogView(_host, _session, inOverlay: true) { OnClose = Close };
        _decor.AddView(_panel, new FrameLayout.LayoutParams(width, height)
        {
            Gravity = GravityFlags.Bottom | GravityFlags.CenterHorizontal
        });
        _panel.Refresh();
        _panel.TranslationY = height;
        _panel.Animate().TranslationY(0f).SetDuration(180).Start();
        _fab.BringToFront();
    }

    void Close()
    {
        var panel = _panel;
        if (panel == null)
            return;

        _panel = null;
        panel.Animate()
            .TranslationY(panel.Height)
            .SetDuration(150)
            .WithEndAction(new Java.Lang.Runnable(() => _decor?.RemoveView(panel)))
            .Start();
    }
}
