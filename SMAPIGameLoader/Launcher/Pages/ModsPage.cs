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
    enum Filter { All, On, Off }

    readonly Context _ctx;
    readonly SheetHost _sheet;
    readonly ModAdapter _adapter;
    readonly TextView _summary;
    readonly EditText _search;
    readonly ImageView _clear;
    readonly TextView[] _chips = new TextView[3];
    readonly LinearLayout _listPanel;
    readonly RecyclerView _list;
    readonly LinearLayout _empty;
    readonly TextView _emptyTitle;
    readonly TextView _emptyBody;
    readonly TextView _emptyAction;
    List<ModInfo> _all = new();
    Filter _filter = Filter.All;
    Action _emptyTap;

    public ModsPage(Context ctx, SheetHost sheet) : base(ctx)
    {
        _ctx = ctx;
        _sheet = sheet;
        Orientation = Orientation.Vertical;
        int edge = Ui.Dp(ctx, 20);

        var header = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
        header.SetGravity(GravityFlags.CenterVertical);
        header.SetPadding(edge, Ui.Dp(ctx, 24), edge, 0);
        header.AddView(Ui.Label(ctx, "Mods", 30, Tokens.Text, medium: true), new LayoutParams(0, Ui.Wrap, 1f));

        var add = Ui.Button(ctx, "Add");
        var plus = Ui.Icon(ctx, "ic_add", Tokens.Text);
        plus.SetBounds(0, 0, Ui.Dp(ctx, 18), Ui.Dp(ctx, 18));
        add.SetCompoundDrawables(plus, null, null, null);
        add.CompoundDrawablePadding = Ui.Dp(ctx, 8);
        add.Click += (_, _) => AddMod();
        header.AddView(add);
        AddView(header, new LayoutParams(Ui.Match, Ui.Wrap));

        _summary = Ui.Label(ctx, "", 14, Tokens.Muted);
        _summary.SetPadding(edge, 0, edge, 0);
        AddView(_summary, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 6) });

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
        _search.SetPadding(Ui.Dp(ctx, 10), Ui.Dp(ctx, 12), Ui.Dp(ctx, 10), Ui.Dp(ctx, 12));
        searchBox.AddView(_search, new LayoutParams(0, Ui.Wrap, 1f));

        _clear = Ui.IconView(ctx, "ic_close", Tokens.Muted, 44);
        _clear.SetPadding(Ui.Dp(ctx, 13), Ui.Dp(ctx, 13), Ui.Dp(ctx, 13), Ui.Dp(ctx, 13));
        _clear.Visibility = ViewStates.Gone;
        _clear.ContentDescription = "Clear search";
        _clear.Click += (_, _) => _search.Text = "";
        searchBox.AddView(_clear);

        _search.TextChanged += (_, _) =>
        {
            _clear.Visibility = string.IsNullOrEmpty(_search.Text) ? ViewStates.Gone : ViewStates.Visible;
            ApplyFilter();
        };
        AddView(searchBox, new LayoutParams(Ui.Match, Ui.Dp(ctx, 46))
        {
            LeftMargin = edge,
            RightMargin = edge,
            TopMargin = Ui.Dp(ctx, 16)
        });

        var chipRow = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
        chipRow.SetPadding(edge, 0, edge, 0);
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
        AddView(chipRow, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 12) });

        var body = new FrameLayout(ctx);
        body.SetPadding(edge, Ui.Dp(ctx, 12), edge, Ui.Dp(ctx, 12));

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
        _empty.SetPadding(Ui.Dp(ctx, 24), 0, Ui.Dp(ctx, 24), Ui.Dp(ctx, 48));
        _empty.AddView(Ui.IconView(ctx, "ic_sprout", null, 72));

        _emptyTitle = Ui.Label(ctx, "", 18, Tokens.Text, medium: true);
        _emptyTitle.Gravity = GravityFlags.Center;
        _empty.AddView(_emptyTitle, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 20) });

        _emptyBody = Ui.Label(ctx, "", 14, Tokens.Muted);
        _emptyBody.Gravity = GravityFlags.Center;
        _emptyBody.SetLineSpacing(0, 1.2f);
        _empty.AddView(_emptyBody, new LayoutParams(Ui.Match, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 8) });

        _emptyAction = Ui.Button(ctx, "", Ui.Kind.Primary);
        _emptyAction.SetMinWidth(Ui.Dp(ctx, 160));
        _emptyAction.Click += (_, _) => _emptyTap?.Invoke();
        _empty.AddView(_emptyAction, new LayoutParams(Ui.Wrap, Ui.Wrap) { TopMargin = Ui.Dp(ctx, 24) });

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

        IEnumerable<ModInfo> items = _all;
        if (_filter == Filter.On)
            items = items.Where(m => m.Enabled);
        else if (_filter == Filter.Off)
            items = items.Where(m => !m.Enabled);

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
            _emptyBody.Text = "Add a mod as a .zip file and the launcher unpacks it into your Mods folder.";
            _emptyAction.Text = "Add a mod";
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
        string[] names = { $"All {_all.Count}", $"On {on}", $"Off {_all.Count - on}" };
        for (int i = 0; i < _chips.Length; i++)
        {
            bool selected = (int)_filter == i;
            _chips[i].Text = names[i];
            _chips[i].SetTextColor(selected ? Tokens.Text : Tokens.Muted);
            _chips[i].Background = selected ? Ui.Box(_ctx, Tokens.Surface2, 8, Tokens.Border) : null;
        }
        _summary.Text = _all.Count == 0 ? "Nothing installed yet" : $"{on} of {_all.Count} turned on";
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

        if (mod.LoadError != null)
        {
            body.AddView(Ui.Label(_ctx, mod.LoadError, 13, Tokens.Red), Gap(14));
        }
        else if (mod.MissingDependencies.Count > 0)
        {
            var needs = "Needs " + string.Join(", ", mod.MissingDependencies) + ". Install or turn them on.";
            body.AddView(Ui.Label(_ctx, needs, 13, Tokens.Gold), Gap(14));
        }

        var details = Ui.Panel(_ctx);
        details.SetPadding(Ui.Dp(_ctx, 14), Ui.Dp(_ctx, 12), Ui.Dp(_ctx, 14), Ui.Dp(_ctx, 12));
        if (mod.UniqueId.Length > 0)
            details.AddView(Ui.Field(_ctx, "Unique ID", mod.UniqueId));
        details.AddView(Ui.Field(_ctx, "Folder", RelativeFolder(mod)), mod.UniqueId.Length > 0 ? Gap(12) : Gap(0));
        body.AddView(details, Gap(18));

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
