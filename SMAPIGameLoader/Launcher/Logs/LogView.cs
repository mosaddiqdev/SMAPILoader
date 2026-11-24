using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using SMAPIGameLoader.Tool;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SMAPIGameLoader.Launcher;

// The terminal. Shared by the in-game overlay and the launcher's last-log sheet.
internal sealed class LogView : LinearLayout
{
    static readonly (string Name, LogLevel Min)[] Filters =
    {
        ("All", LogLevel.Trace),
        ("Info", LogLevel.Info),
        ("Warn", LogLevel.Warn),
        ("Error", LogLevel.Error),
    };

    readonly Context _ctx;
    readonly LogSession _session;
    readonly RecyclerView _list;
    readonly LogAdapter _adapter = new();
    readonly TextView[] _chips = new TextView[Filters.Length];
    readonly TextView _empty;
    int _filter = 1;
    List<LogEntry> _shown = new();

    public Action OnClose { get; set; }
    public string EmptyText { get; set; } = "Waiting for SMAPI to start…";

    public LogView(Context ctx, LogSession session, bool inOverlay) : base(ctx)
    {
        _ctx = ctx;
        _session = session;
        Orientation = Orientation.Vertical;

        if (inOverlay)
        {
            // swallow touches so they don't reach the game underneath
            Clickable = true;
            SetBackgroundColor(Color.ParseColor("#F2100F0E"));
            AddView(Ui.Divider(ctx));
        }
        int edge = Ui.Dp(ctx, 14);

        var top = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
        top.SetGravity(GravityFlags.CenterVertical);
        top.SetPadding(edge, Ui.Dp(ctx, 8), inOverlay ? 0 : edge, 0);
        top.AddView(Ui.Label(ctx, "SMAPI log", 17, Tokens.Text, medium: true), new LayoutParams(0, Ui.Wrap, 1f));

        var copy = Small("Copy");
        copy.Click += (_, _) => Copy();
        top.AddView(copy, new LayoutParams(Ui.Wrap, Ui.Wrap) { RightMargin = Ui.Dp(ctx, 8) });

        var upload = Small("Upload");
        upload.Click += (_, _) => LogParser.OnClickUploadLog(this, EventArgs.Empty);
        top.AddView(upload);

        if (inOverlay)
        {
            var close = Ui.IconView(ctx, "ic_close", Tokens.Muted, 44);
            close.SetPadding(Ui.Dp(ctx, 13), Ui.Dp(ctx, 13), Ui.Dp(ctx, 13), Ui.Dp(ctx, 13));
            close.ContentDescription = "Close log";
            close.Click += (_, _) => OnClose?.Invoke();
            top.AddView(close);
        }
        AddView(top, new LayoutParams(Ui.Match, Ui.Wrap));

        var chipRow = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
        chipRow.SetPadding(edge, Ui.Dp(ctx, 8), edge, Ui.Dp(ctx, 8));
        for (int i = 0; i < _chips.Length; i++)
        {
            int index = i;
            var chip = Ui.Label(ctx, "", 13, Tokens.Muted, medium: true);
            chip.Gravity = GravityFlags.Center;
            chip.SetPadding(Ui.Dp(ctx, 12), 0, Ui.Dp(ctx, 12), 0);
            chip.Clickable = true;
            chip.Click += (_, _) =>
            {
                _filter = index;
                Refresh();
            };
            chipRow.AddView(chip, new LayoutParams(Ui.Wrap, Ui.Dp(ctx, 32)) { RightMargin = Ui.Dp(ctx, 8) });
            _chips[i] = chip;
        }
        AddView(chipRow, new LayoutParams(Ui.Match, Ui.Wrap));

        var body = new FrameLayout(ctx);
        _list = new RecyclerView(ctx);
        _list.SetLayoutManager(new LinearLayoutManager(ctx));
        _list.SetAdapter(_adapter);
        _list.SetPadding(edge, 0, edge, Ui.Dp(ctx, 10));
        _list.SetClipToPadding(false);
        body.AddView(_list, new FrameLayout.LayoutParams(Ui.Match, Ui.Match));

        _empty = Ui.Label(ctx, EmptyText, 14, Tokens.Muted);
        _empty.Gravity = GravityFlags.Center;
        _empty.SetPadding(Ui.Dp(ctx, 32), 0, Ui.Dp(ctx, 32), 0);
        body.AddView(_empty, new FrameLayout.LayoutParams(Ui.Match, Ui.Match));
        AddView(body, new LayoutParams(Ui.Match, 0, 1f));

        Refresh();
    }

    TextView Small(string text)
    {
        var button = Ui.Button(_ctx, text);
        button.SetTextSize(ComplexUnitType.Sp, 13);
        button.SetMinHeight(Ui.Dp(_ctx, 34));
        button.SetPadding(Ui.Dp(_ctx, 12), 0, Ui.Dp(_ctx, 12), 0);
        return button;
    }

    public void Refresh()
    {
        // keep following the newest line only if the reader is already at the bottom
        bool follow = !_list.CanScrollVertically(1);

        var min = Filters[_filter].Min;
        _shown = _session.Lines.Where(e => e.Level >= min).ToList();
        _adapter.Replace(_shown);
        UpdateChips();

        _empty.Text = EmptyText;
        _empty.Visibility = _shown.Count == 0 ? ViewStates.Visible : ViewStates.Gone;

        if (follow && _shown.Count > 0)
            _list.ScrollToPosition(_shown.Count - 1);
    }

    void UpdateChips()
    {
        string[] names =
        {
            "All",
            "Info",
            _session.Warnings > 0 ? $"Warn {_session.Warnings}" : "Warn",
            _session.Errors > 0 ? $"Error {_session.Errors}" : "Error",
        };
        for (int i = 0; i < _chips.Length; i++)
        {
            bool selected = i == _filter;
            _chips[i].Text = names[i];
            _chips[i].SetTextColor(selected ? Tokens.Text : Tokens.Muted);
            _chips[i].Background = selected ? Ui.Box(_ctx, Tokens.Surface2, 8, Tokens.Border) : null;
        }
    }

    void Copy()
    {
        var text = LogSession.ToText(_shown);
        if (text.Length > 400_000)
            text = text.Substring(text.Length - 400_000);

        var clipboard = (ClipboardManager)_ctx.GetSystemService(Context.ClipboardService);
        clipboard.PrimaryClip = ClipData.NewPlainText("SMAPI log", text);
        ToastNotifyTool.Notify($"Copied {_shown.Count} lines");
    }

    sealed class LogAdapter : RecyclerView.Adapter
    {
        List<LogEntry> _items = new();

        public void Replace(List<LogEntry> items)
        {
            _items = items;
            NotifyDataSetChanged();
        }

        public override int ItemCount => _items.Count;

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var ctx = parent.Context;
            var line = Ui.Label(ctx, "", 11, Tokens.Text, mono: true);
            line.SetPadding(0, Ui.Dp(ctx, 2), 0, Ui.Dp(ctx, 2));
            line.SetLineSpacing(0, 1.1f);
            line.LayoutParameters = new RecyclerView.LayoutParams(Ui.Match, Ui.Wrap);
            return new LineHolder(line);
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var entry = _items[position];
            var view = (TextView)holder.ItemView;
            var text = new SpannableString(entry.Raw);

            // timestamp and source stay quiet, the level word and the message carry the colour
            if (entry.HeaderLength > 0)
            {
                int end = Math.Min(entry.HeaderLength, entry.Raw.Length);
                text.SetSpan(new ForegroundColorSpan(Tokens.Faint), 0, end, SpanTypes.ExclusiveExclusive);
                text.SetSpan(new ForegroundColorSpan(ColorFor(entry.Level)), entry.LevelStart, entry.LevelEnd, SpanTypes.ExclusiveExclusive);
            }
            view.TextFormatted = text;
            view.SetTextColor(ColorFor(entry.Level));
        }

        static Color ColorFor(LogLevel level) => level switch
        {
            LogLevel.Error => Tokens.Red,
            LogLevel.Warn => Tokens.Gold,
            LogLevel.Alert => Tokens.Green,
            LogLevel.Info => Tokens.Text,
            _ => Tokens.Faint,
        };
    }

    sealed class LineHolder : RecyclerView.ViewHolder
    {
        public LineHolder(View view) : base(view) { }
    }
}
