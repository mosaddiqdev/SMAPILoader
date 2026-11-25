using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.RecyclerView.Widget;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SMAPIGameLoader.Launcher.Pages;

internal sealed class ModsPage : LinearLayout
{
    enum Filter { All, On, Off, WontLoad }

    readonly Context _ctx;
    readonly SheetHost _sheet;
    readonly ModAdapter _adapter;
    readonly TextView _summary;
    readonly EditText _search;
    readonly ImageView _clear;
    readonly TextView[] _chips = new TextView[4];
    readonly LinearLayout _listPanel;
    readonly RecyclerView _list;
    readonly LinearLayout _empty;
    readonly TextView _emptyTitle;
    readonly TextView _emptyBody;
    readonly TextView _emptyAction;
    List<ModInfo> _all = new();
    Filter _filter = Filter.All;
    Action _emptyTap;

    // turned on but can't load: the only mods that actually need your attention
    static bool WontLoad(ModInfo mod) => mod.Enabled && mod.Compat?.Level == CompatLevel.Incompatible;

    public ModsPage(Context ctx, SheetHost sheet) : base(ctx)
    {
        _ctx = ctx;
        _sheet = sheet;
        Orientation = Orientation.Vertical;
        int edge = Ui.Dp(ctx, 20);

        // Landscape is short. Title, search, filters and Add share one row so the list keeps the height.
        bool landscape = ctx.Resources.Configuration.Orientation == Android.Content.Res.Orientation.Landscape;

        var title = Ui.Label(ctx, "Mods", landscape ? 22 : 30, Tokens.Text, medium: true);

        var add = Ui.Button(ctx, "Add");
        var plus = Ui.Icon(ctx, "ic_add", Tokens.Text);
        plus.SetBounds(0, 0, Ui.Dp(ctx, 18), Ui.Dp(ctx, 18));
        add.SetCompoundDrawables(plus, null, null, null);
        add.CompoundDrawablePadding = Ui.Dp(ctx, 8);
        add.Click += (_, _) => AddMod();

        _summary = Ui.Label(ctx, "", 14, Tokens.Muted);
        _summary.SetPadding(edge, 0, edge, 0);

        int searchPad = Ui.Dp(ctx, landscape ? 8 : 12);
        var searchBox = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
        searchBox.SetGravity(GravityFlags.CenterVertical);
        searchBox.Background = Ui.Box(ctx, Tokens.Surface, 10, Tokens.Border);
        searchBox.SetPadding(Ui.Dp(ctx, 12), 0, 0, 0);
        searchBox.AddView(Ui.IconView(ctx, "ic_search", Tokens.Faint, 18));

        _search = new EditText(ctx) { Hint = "Search mods" };
        _search.SetSingleLine(true);
        _search.SetTextSize(ComplexUnitType.Sp, 15);
        _search.SetTextColor(Tokens.Text);
        _search.SetHintTextColor(Tokens.Faint);
        _search.Background = null;
        _search.SetPadding(Ui.Dp(ctx, 10), searchPad, Ui.Dp(ctx, 10), searchPad);
        searchBox.AddView(_search, new LayoutParams(0, Ui.Wrap, 1f));

        _clear = Ui.IconView(ctx, "ic_close", Tokens.Muted, 40);
        _clear.SetPadding(Ui.Dp(ctx, 11), Ui.Dp(ctx, 11), Ui.Dp(ctx, 11), Ui.Dp(ctx, 11));
        _clear.Visibility = ViewStates.Gone;
        _clear.ContentDescription = "Clear search";
        _clear.Click += (_, _) => _search.Text = "";
        searchBox.AddView(_clear);

        _search.TextChanged += (_, _) =>
        {
            _clear.Visibility = string.IsNullOrEmpty(_search.Text) ? ViewStates.Gone : ViewStates.Visible;
            ApplyFilter();
        };

        var chipRow = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
        chipRow.SetPadding(landscape ? 0 : edge, 0, landscape ? 0 : edge, 0);
        for (int i = 0; i < _chips.Length; i++)
        {
            var filter = (Filter)i;
            var chip = Ui.Label(ctx, "", 13, Tokens.Muted, medium: true);
            chip.Gravity = GravityFlags.Center;
            chip.SetPadding(Ui.Dp(ctx, 12), 0, Ui.Dp(ctx, 12), 0);
            chip.Clickable = true;
            chip.Click += (_, _) =>
            {
                _filter = filter;
                ApplyFilter();
            };
            chipRow.AddView(chip, new LayoutParams(Ui.Wrap, Ui.Dp(ctx, 32)) { RightMargin = Ui.Dp(ctx, 8) });
            _chips[i] = chip;
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
            bar.AddView(chipRow, new LayoutParams(Ui.Wrap, Ui.Wrap));
            bar.AddView(add, new LayoutParams(Ui.Wrap, Ui.Wrap) { LeftMargin = Ui.Dp(ctx, 4) });
            AddView(bar, new LayoutParams(Ui.Match, Ui.Wrap));
        }
        else
        {
            var header = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
            header.SetGravity(GravityFlags.CenterVertical);
            header.SetPadding(edge, Ui.Dp(ctx, 24), edge, 0);
            header.AddView(title, new LayoutParams(0, Ui.Wrap, 1f));
            header.AddView(add);
            AddView(header, new LayoutParams(Ui.Match, Ui.Wrap));

            AddView(_summary, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 6) });
            AddView(searchBox, new LayoutParams(Ui.Match, Ui.Dp(ctx, 46))
            {
                LeftMargin = edge,
                RightMargin = edge,
                TopMargin = Ui.Dp(ctx, 16)
            });
            AddView(chipRow, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 12) });
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
        _empty.SetPadding(Ui.Dp(ctx, 24), 0, Ui.Dp(ctx, 24), Ui.Dp(ctx, landscape ? 8 : 48));
        _empty.AddView(Ui.IconView(ctx, "ic_sprout", null, landscape ? 48 : 72));

        _emptyTitle = Ui.Label(ctx, "", 18, Tokens.Text, medium: true);
        _emptyTitle.Gravity = GravityFlags.Center;
        _empty.AddView(_emptyTitle, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, landscape ? 12 : 20) });

        _emptyBody = Ui.Label(ctx, "", 14, Tokens.Muted);
        _emptyBody.Gravity = GravityFlags.Center;
        _emptyBody.SetLineSpacing(0, 1.2f);
        _empty.AddView(_emptyBody, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 8) });

        _emptyAction = Ui.Button(ctx, "", Ui.Kind.Primary);
        _emptyAction.SetMinWidth(Ui.Dp(ctx, 160));
        _emptyAction.Click += (_, _) => _emptyTap?.Invoke();
        _empty.AddView(_emptyAction, new LayoutParams(Ui.Wrap, Ui.Wrap) { TopMargin = Ui.Dp(ctx, landscape ? 16 : 24) });

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

        // the "Won't load" chip disappears once nothing is wrong, so don't stay stuck on it
        if (_filter == Filter.WontLoad && !_all.Any(WontLoad))
            _filter = Filter.All;

        IEnumerable<ModInfo> items = _all;
        if (_filter == Filter.On)
            items = items.Where(m => m.Enabled);
        else if (_filter == Filter.Off)
            items = items.Where(m => !m.Enabled);
        else if (_filter == Filter.WontLoad)
            items = items.Where(WontLoad);

        if (query.Length > 0)
        {
            items = items.Where(m =>
                m.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                m.Author.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                m.UniqueId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var shown = items
            .OrderByDescending(m => m.Enabled)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _adapter.Replace(shown);
        UpdateChips();

        bool nothing = shown.Count == 0;
        _listPanel.Visibility = nothing ? ViewStates.Gone : ViewStates.Visible;
        _empty.Visibility = nothing ? ViewStates.Visible : ViewStates.Gone;
        if (!nothing)
            return;

        if (_all.Count == 0)
        {
            _emptyTitle.Text = "No mods yet";
            _emptyBody.Text = "Add mod zips and the launcher unpacks them into your Mods folder. You can pick several at once.";
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

    void UpdateChips()
    {
        int on = _all.Count(m => m.Enabled);
        int broken = _all.Count(WontLoad);
        string[] names = { $"All {_all.Count}", $"On {on}", $"Off {_all.Count - on}", $"Won’t load {broken}" };
        for (int i = 0; i < _chips.Length; i++)
        {
            bool selected = (int)_filter == i;
            _chips[i].Text = names[i];
            _chips[i].SetTextColor(selected ? Tokens.Text : Tokens.Muted);
            _chips[i].Background = selected ? Ui.Box(_ctx, Tokens.Surface2, 8, Tokens.Border) : null;
        }
        _chips[(int)Filter.WontLoad].Visibility = broken > 0 ? ViewStates.Visible : ViewStates.Gone;

        _summary.Text = _all.Count == 0
            ? "Nothing installed yet"
            : broken > 0 ? $"{on} of {_all.Count} turned on, {broken} won’t load" : $"{on} of {_all.Count} turned on";
    }

    // The list keeps its order after a toggle so the row doesn't jump away under your finger.
    void OnToggled(ModInfo mod)
    {
        ModLibrary.Resolve(_all);
        UpdateChips();
        _list.Post(() => _adapter.NotifyDataSetChanged());
    }

    void AddMod() => ModInstaller.OnClickInstallMod(Reload);

    LayoutParams Gap(int dp) => new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(_ctx, dp) };

    void OpenDetail(ModInfo mod)
    {
        var body = Ui.SheetBody(_ctx);
        body.AddView(Ui.Label(_ctx, mod.Name, 22, Tokens.Text, medium: true));

        var byline = string.Join(" · ", new[]
        {
            mod.Author.Length > 0 ? "by " + mod.Author : "",
            mod.Version.Length > 0 ? "v" + mod.Version : ""
        }.Where(s => s.Length > 0));
        if (byline.Length > 0)
            body.AddView(Ui.Label(_ctx, byline, 14, Tokens.Muted), Gap(6));

        if (mod.Description.Length > 0)
        {
            var description = Ui.Label(_ctx, mod.Description, 14, Tokens.Text);
            description.SetLineSpacing(0, 1.2f);
            description.SetMaxLines(6);
            description.Ellipsize = TextUtils.TruncateAt.End;
            body.AddView(description, Gap(14));
        }

        if (mod.Compat != null)
        {
            bool ok = mod.Compat.Level == CompatLevel.Compatible;
            var compat = Ui.Panel(_ctx);
            compat.SetPadding(Ui.Dp(_ctx, 14), Ui.Dp(_ctx, 12), Ui.Dp(_ctx, 14), Ui.Dp(_ctx, 12));
            compat.AddView(CompatUi.Badge(_ctx, mod.Compat), new LayoutParams(Ui.Wrap, Ui.Wrap));

            foreach (var line in ok ? mod.Compat.Passed : mod.Compat.Problems)
            {
                var text = Ui.Label(_ctx, line, 13, ok ? Tokens.Muted : Tokens.Text);
                text.SetLineSpacing(0, 1.15f);
                compat.AddView(text, Gap(8));
            }
            body.AddView(compat, Gap(16));
        }

        var details = Ui.Panel(_ctx);
        details.SetPadding(Ui.Dp(_ctx, 14), Ui.Dp(_ctx, 12), Ui.Dp(_ctx, 14), Ui.Dp(_ctx, 12));
        if (mod.UniqueId.Length > 0)
            details.AddView(Ui.Field(_ctx, "Unique ID", mod.UniqueId));
        details.AddView(Ui.Field(_ctx, "Folder", RelativeFolder(mod)), mod.UniqueId.Length > 0 ? Gap(12) : Gap(0));
        body.AddView(details, Gap(12));

        var open = Ui.Button(_ctx, "Open folder");
        open.Click += (_, _) => FileTool.OpenAppFiles(mod.ModFolderPath);
        var delete = Ui.Button(_ctx, "Delete", Ui.Kind.Danger);
        delete.Click += (_, _) => ConfirmDelete(mod);

        var actions = new LinearLayout(_ctx) { Orientation = Orientation.Horizontal };
        actions.AddView(open, new LayoutParams(0, Ui.Wrap, 1f) { RightMargin = Ui.Dp(_ctx, 12) });
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
