using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.RecyclerView.Widget;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SMAPIGameLoader.Launcher;

internal sealed class ModsPage : LinearLayout
{
    enum Filter { All, On, Off, Issues }

    readonly Context _ctx;
    readonly SheetHost _sheet;
    readonly ModAdapter _adapter;
    readonly TextView _summary;
    readonly EditText _search;
    readonly ImageView _clear;
    readonly TextView[] _segments = new TextView[4];
    readonly LinearLayout _listPanel;
    readonly RecyclerView _list;
    readonly LinearLayout _empty;
    readonly TextView _emptyTitle;
    readonly TextView _emptyBody;
    readonly TextView _emptyAction;
    List<ModInfo> _all = new();
    Filter _filter = Filter.All;
    Action _emptyTap;

    // turned on but can't load: the only mods that need your attention
    static bool Issue(ModInfo mod) => mod.Enabled && mod.Compat?.Level == CompatLevel.Incompatible;

    public ModsPage(Context ctx, SheetHost sheet) : base(ctx)
    {
        _ctx = ctx;
        _sheet = sheet;
        Orientation = Orientation.Vertical;
        int edge = Ui.Dp(ctx, 20);

        // Landscape is short. Title, search, filter and Add share one row so the list keeps the height.
        bool landscape = ctx.Resources.Configuration.Orientation == Android.Content.Res.Orientation.Landscape;

        var title = Ui.Label(ctx, "Mods", landscape ? 26 : 34, Tokens.Text, Ui.Face.DisplayBold);

        var add = Ui.Button(ctx, "Add", Ui.Kind.Primary, 38);
        var plus = Ui.Icon(ctx, "ic_plus", Tokens.Bg);
        plus.SetBounds(0, 0, Ui.Dp(ctx, 16), Ui.Dp(ctx, 16));
        add.SetCompoundDrawables(plus, null, null, null);
        add.CompoundDrawablePadding = Ui.Dp(ctx, 6);
        add.SetPadding(Ui.Dp(ctx, 12), 0, Ui.Dp(ctx, 16), 0);
        add.Click += (_, _) => AddMod();

        _summary = Ui.Label(ctx, "", 13, Tokens.Muted);
        _summary.SetPadding(edge, 0, edge, 0);

        int searchPad = Ui.Dp(ctx, landscape ? 8 : 11);
        var searchBox = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
        searchBox.SetGravity(GravityFlags.CenterVertical);
        searchBox.Background = Ui.Box(ctx, Tokens.Surface, 12, Tokens.Line);
        searchBox.SetPadding(Ui.Dp(ctx, 12), 0, 0, 0);
        searchBox.AddView(Ui.IconView(ctx, "ic_search", Tokens.Faint, 17));

        _search = new EditText(ctx) { Hint = "Search mods" };
        _search.SetSingleLine(true);
        _search.SetTextSize(ComplexUnitType.Sp, 14);
        _search.SetTextColor(Tokens.Text);
        _search.SetHintTextColor(Tokens.Faint);
        _search.Background = null;
        _search.SetPadding(Ui.Dp(ctx, 10), searchPad, Ui.Dp(ctx, 10), searchPad);
        searchBox.AddView(_search, new LayoutParams(0, Ui.Wrap, 1f));

        _clear = Ui.IconView(ctx, "ic_close", Tokens.Muted, 40);
        _clear.SetPadding(Ui.Dp(ctx, 12), Ui.Dp(ctx, 12), Ui.Dp(ctx, 12), Ui.Dp(ctx, 12));
        _clear.Visibility = ViewStates.Gone;
        _clear.ContentDescription = "Clear search";
        _clear.Click += (_, _) => _search.Text = "";
        searchBox.AddView(_clear);

        _search.TextChanged += (_, _) =>
        {
            _clear.Visibility = string.IsNullOrEmpty(_search.Text) ? ViewStates.Gone : ViewStates.Visible;
            ApplyFilter();
        };

        // one segmented control instead of loose chips
        var segmented = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
        segmented.Background = Ui.Box(ctx, Tokens.Surface, 11, Tokens.Line);
        segmented.SetPadding(Ui.Dp(ctx, 3), Ui.Dp(ctx, 3), Ui.Dp(ctx, 3), Ui.Dp(ctx, 3));
        for (int i = 0; i < _segments.Length; i++)
        {
            var filter = (Filter)i;
            var segment = Ui.Label(ctx, "", 13, Tokens.Dim, Ui.Face.Medium);
            segment.Gravity = GravityFlags.Center;
            segment.SetPadding(Ui.Dp(ctx, landscape ? 12 : 0), Ui.Dp(ctx, 7), Ui.Dp(ctx, landscape ? 12 : 0), Ui.Dp(ctx, 7));
            segment.Clickable = true;
            segment.Click += (_, _) =>
            {
                _filter = filter;
                ApplyFilter();
            };
            segmented.AddView(segment, landscape ? new LayoutParams(Ui.Wrap, Ui.Wrap) : new LayoutParams(0, Ui.Wrap, 1f));
            _segments[i] = segment;
        }

        if (landscape)
        {
            var bar = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
            bar.SetGravity(GravityFlags.CenterVertical);
            bar.SetPadding(edge, Ui.Dp(ctx, 10), edge, 0);
            bar.AddView(title, new LayoutParams(Ui.Wrap, Ui.Wrap));
            bar.AddView(searchBox, new LayoutParams(0, Ui.Dp(ctx, 40), 1f)
            {
                LeftMargin = Ui.Dp(ctx, 16),
                RightMargin = Ui.Dp(ctx, 12)
            });
            bar.AddView(segmented, new LayoutParams(Ui.Wrap, Ui.Wrap) { RightMargin = Ui.Dp(ctx, 12) });
            bar.AddView(add, new LayoutParams(Ui.Wrap, Ui.Wrap));
            AddView(bar, new LayoutParams(Ui.Match, Ui.Wrap));
        }
        else
        {
            var header = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
            header.SetGravity(GravityFlags.CenterVertical);
            header.SetPadding(edge, Ui.Dp(ctx, 20), edge, 0);
            header.AddView(title, new LayoutParams(0, Ui.Wrap, 1f));
            header.AddView(add, new LayoutParams(Ui.Wrap, Ui.Wrap));
            AddView(header, new LayoutParams(Ui.Match, Ui.Wrap));

            AddView(_summary, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 6) });
            AddView(searchBox, new LayoutParams(Ui.Match, Ui.Dp(ctx, 44))
            {
                LeftMargin = edge,
                RightMargin = edge,
                TopMargin = Ui.Dp(ctx, 14)
            });
            AddView(segmented, new LayoutParams(Ui.Match, Ui.Wrap)
            {
                LeftMargin = edge,
                RightMargin = edge,
                TopMargin = Ui.Dp(ctx, 10)
            });
        }

        var body = new FrameLayout(ctx);
        body.SetPadding(edge, Ui.Dp(ctx, landscape ? 8 : 12), edge, Ui.Dp(ctx, 12));

        _adapter = new ModAdapter(ctx, OnToggled, OpenDetail);
        _list = new RecyclerView(ctx);
        _list.SetLayoutManager(new LinearLayoutManager(ctx));
        _list.SetAdapter(_adapter);
        _list.OverScrollMode = OverScrollMode.Never;
        _listPanel = Ui.Panel(ctx);
        _listPanel.AddView(_list, new LayoutParams(Ui.Match, Ui.Wrap));
        body.AddView(_listPanel, new FrameLayout.LayoutParams(Ui.Match, Ui.Wrap));

        _empty = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        _empty.SetGravity(GravityFlags.Center);
        _empty.SetPadding(Ui.Dp(ctx, 28), 0, Ui.Dp(ctx, 28), Ui.Dp(ctx, landscape ? 8 : 48));
        _empty.AddView(Ui.IconView(ctx, "ic_grid", Tokens.LineStrong, landscape ? 32 : 40));

        _emptyTitle = Ui.Label(ctx, "", 26, Tokens.Text, Ui.Face.DisplayBold);
        _emptyTitle.Gravity = GravityFlags.Center;
        _empty.AddView(_emptyTitle, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, landscape ? 10 : 18) });

        _emptyBody = Ui.Label(ctx, "", 14, Tokens.Muted);
        _emptyBody.Gravity = GravityFlags.Center;
        _emptyBody.SetLineSpacing(0, 1.2f);
        _empty.AddView(_emptyBody, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 8) });

        _emptyAction = Ui.Button(ctx, "", Ui.Kind.Primary, 46);
        _emptyAction.SetPadding(Ui.Dp(ctx, 24), 0, Ui.Dp(ctx, 24), 0);
        _emptyAction.Click += (_, _) => _emptyTap?.Invoke();
        _empty.AddView(_emptyAction, new LayoutParams(Ui.Wrap, Ui.Wrap) { TopMargin = Ui.Dp(ctx, landscape ? 14 : 22) });

        _empty.Visibility = ViewStates.Gone;
        body.AddView(_empty, new FrameLayout.LayoutParams(Ui.Match, Ui.Match));
        AddView(body, new LayoutParams(Ui.Match, 0, 1f));

        Reload();
    }

    public void Reload()
    {
        _all = ModLibrary.Scan();
        ApplyFilter();
    }

    void ApplyFilter()
    {
        var query = (_search.Text ?? "").Trim();

        // the Issues segment disappears once nothing is wrong, so don't stay stuck on it
        if (_filter == Filter.Issues && !_all.Any(Issue))
            _filter = Filter.All;

        IEnumerable<ModInfo> items = _all;
        if (_filter == Filter.On)
            items = items.Where(m => m.Enabled);
        else if (_filter == Filter.Off)
            items = items.Where(m => !m.Enabled);
        else if (_filter == Filter.Issues)
            items = items.Where(Issue);

        if (query.Length > 0)
        {
            items = items.Where(m =>
                m.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                m.Author.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                m.UniqueId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        // problems first, then A to Z. Off mods stay in the list, just dimmed.
        var shown = items
            .OrderByDescending(Issue)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _adapter.Replace(shown);
        UpdateHeader();

        bool nothing = shown.Count == 0;
        _listPanel.Visibility = nothing ? ViewStates.Gone : ViewStates.Visible;
        _empty.Visibility = nothing ? ViewStates.Visible : ViewStates.Gone;
        if (!nothing)
            return;

        if (_all.Count == 0)
        {
            _emptyTitle.Text = "No mods yet";
            _emptyBody.Text = "Add mod zips and the launcher unpacks them for you. You can pick several at once.";
            _emptyAction.Text = "Add mods";
            _emptyTap = AddMod;
        }
        else
        {
            _emptyTitle.Text = "No results";
            _emptyBody.Text = query.Length > 0 ? $"Nothing matches “{query}”." : "No mods in this view.";
            _emptyAction.Text = "Clear filters";
            _emptyTap = () =>
            {
                _filter = Filter.All;
                _search.Text = "";
                ApplyFilter();
            };
        }
    }

    void UpdateHeader()
    {
        int on = _all.Count(m => m.Enabled);
        int off = _all.Count - on;
        int issues = _all.Count(Issue);

        string[] names = { $"All {_all.Count}", $"On {on}", $"Off {off}", $"Issues {issues}" };
        for (int i = 0; i < _segments.Length; i++)
        {
            bool selected = (int)_filter == i;
            _segments[i].Text = names[i];
            _segments[i].SetTextColor(selected ? Tokens.Text : Tokens.Dim);
            _segments[i].Background = selected ? Ui.Box(_ctx, Tokens.SegmentOn, 8, Tokens.LineStrong) : null;
        }
        _segments[(int)Filter.Issues].Visibility = issues > 0 ? ViewStates.Visible : ViewStates.Gone;

        if (_all.Count == 0)
        {
            _summary.Text = "Nothing installed yet";
            return;
        }

        string counts = $"{on} on · {off} off";
        if (issues == 0)
        {
            _summary.Text = counts;
            return;
        }

        var text = new SpannableString(counts + $" · {issues} won’t load");
        text.SetSpan(new ForegroundColorSpan(Tokens.Danger), counts.Length + 3, text.Length(), SpanTypes.ExclusiveExclusive);
        _summary.TextFormatted = text;
    }

    // The list keeps its order after a toggle so the row doesn't jump away under your finger.
    void OnToggled(ModInfo mod)
    {
        ModLibrary.Resolve(_all);
        UpdateHeader();
        _list.Post(() => _adapter.NotifyDataSetChanged());
    }

    void AddMod() => ModInstaller.OnClickInstallMod(Reload);

    LayoutParams Gap(int dp) => new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(_ctx, dp) };

    void OpenDetail(ModInfo mod)
    {
        var body = Ui.SheetBody(_ctx);
        body.AddView(Ui.Label(_ctx, mod.Name, 26, Tokens.Text, Ui.Face.DisplayBold));

        var byline = string.Join(" · ", new[]
        {
            mod.Author.Length > 0 ? "by " + mod.Author : "",
            mod.Version.Length > 0 ? "v" + mod.Version : ""
        }.Where(s => s.Length > 0));
        if (byline.Length > 0)
            body.AddView(Ui.Label(_ctx, byline, 13, Tokens.Muted), Gap(4));

        if (mod.Compat != null)
        {
            bool ok = mod.Compat.Level == CompatLevel.Compatible;
            var compat = Ui.Panel(_ctx);
            compat.SetPadding(Ui.Dp(_ctx, 14), Ui.Dp(_ctx, 12), Ui.Dp(_ctx, 14), Ui.Dp(_ctx, 12));
            compat.AddView(CompatUi.Pill(_ctx, mod.Compat), new LayoutParams(Ui.Wrap, Ui.Wrap));

            foreach (var line in ok ? mod.Compat.Passed : mod.Compat.Problems)
            {
                var text = Ui.Label(_ctx, line, 13, ok ? Tokens.Muted : Tokens.TextSoft);
                text.SetLineSpacing(0, 1.15f);
                compat.AddView(text, Gap(8));
            }
            body.AddView(compat, Gap(14));
        }

        if (mod.Description.Length > 0)
        {
            var description = Ui.Label(_ctx, mod.Description, 14, Color.ParseColor("#BDBCB6"));
            description.SetLineSpacing(0, 1.2f);
            description.SetMaxLines(6);
            description.Ellipsize = TextUtils.TruncateAt.End;
            body.AddView(description, Gap(14));
        }

        body.AddView(Ui.Divider(_ctx), Gap(16));
        if (mod.UniqueId.Length > 0)
            body.AddView(Ui.Field(_ctx, "Unique ID", mod.UniqueId), Gap(12));
        body.AddView(Ui.Field(_ctx, "Folder", RelativeFolder(mod)), Gap(12));

        var open = Ui.Button(_ctx, "Open folder");
        open.Click += (_, _) => FileTool.OpenAppFiles(mod.ModFolderPath);
        var delete = Ui.Button(_ctx, "Delete", Ui.Kind.Danger);
        delete.Click += (_, _) => ConfirmDelete(mod);

        var actions = new LinearLayout(_ctx) { Orientation = Orientation.Horizontal };
        actions.AddView(open, new LayoutParams(0, Ui.Wrap, 1f) { RightMargin = Ui.Dp(_ctx, 10) });
        actions.AddView(delete, new LayoutParams(0, Ui.Wrap, 1f));
        body.AddView(actions, Gap(20));

        _sheet.Show(body);
    }

    static string RelativeFolder(ModInfo mod)
    {
        var path = mod.ModFolderPath ?? "";
        int at = path.IndexOf("/Mods/", StringComparison.Ordinal);
        return at >= 0 ? path.Substring(at + 6) : path;
    }

    void ConfirmDelete(ModInfo mod)
    {
        new AlertDialog.Builder(_ctx)
            .SetTitle($"Delete {mod.Name}?")
            .SetMessage("This removes the mod’s folder from your device.")
            .SetPositiveButton("Delete", (s, e) =>
            {
                if (ModInstaller.TryDeleteMod(mod.ModFolderPath))
                {
                    _sheet.Hide();
                    Reload();
                }
            })
            .SetNegativeButton("Cancel", (s, e) => { })
            .Show();
    }
}
