using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.Widget;
using AndroidX.RecyclerView.Widget;
using System;
using System.IO;
using System.Linq;

// Android.Content.Res and Android.Widget both define Orientation
using Orientation = Android.Widget.Orientation;

namespace SMAPIGameLoader.Launcher;

// One mod: name, a compatibility label next to author and version, and the switch.
internal class ModViewHolder : RecyclerView.ViewHolder
{
    readonly TextView _name;
    readonly TextView _meta;
    readonly TextView _badge;
    readonly LinearLayout _badgeHolder;
    readonly SwitchCompat _toggle;
    readonly LinearLayout _text;
    readonly View _divider;
    readonly Action<ModInfo> _onToggled;
    ModInfo _mod;
    bool _binding;

    public static ModViewHolder Create(Context ctx, Action<ModInfo> onToggled, Action<ModInfo> onOpen)
    {
        var root = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        root.LayoutParameters = new RecyclerView.LayoutParams(Ui.Match, Ui.Wrap);

        var row = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(Ui.Dp(ctx, 16), Ui.Dp(ctx, 8), Ui.Dp(ctx, 12), Ui.Dp(ctx, 8));

        var text = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        var name = Ui.Label(ctx, "", 15, Tokens.Text, Ui.Face.Medium);
        name.SetSingleLine(true);
        name.Ellipsize = TextUtils.TruncateAt.End;
        text.AddView(name);

        // second line: compatibility label, then author and version
        var line = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
        line.SetGravity(GravityFlags.CenterVertical);

        var badge = Ui.Label(ctx, "", 11, Tokens.Ok, Ui.Face.Medium);
        var badgeHolder = new LinearLayout(ctx) { Clickable = true };
        badgeHolder.SetPadding(0, Ui.Dp(ctx, 5), Ui.Dp(ctx, 4), Ui.Dp(ctx, 5));
        badgeHolder.AddView(badge);
        line.AddView(badgeHolder);

        var meta = Ui.Label(ctx, "", 12, Tokens.Dim);
        meta.SetSingleLine(true);
        meta.Ellipsize = TextUtils.TruncateAt.End;
        line.AddView(meta, new LinearLayout.LayoutParams(0, Ui.Wrap, 1f));

        text.AddView(line, new LinearLayout.LayoutParams(Ui.Match, Ui.Wrap));
        row.AddView(text, new LinearLayout.LayoutParams(0, Ui.Wrap, 1f) { RightMargin = Ui.Dp(ctx, 12) });

        var toggle = new SwitchCompat(ctx);
        var states = new[] { new[] { Android.Resource.Attribute.StateChecked }, new int[0] };
        toggle.TrackTintList = new ColorStateList(states, new[] { Tokens.Text.ToArgb(), Tokens.Track.ToArgb() });
        toggle.ThumbTintList = new ColorStateList(states, new[] { Tokens.Bg.ToArgb(), Tokens.Faint.ToArgb() });
        row.AddView(toggle);

        var divider = Ui.Divider(ctx);
        root.AddView(row, new LinearLayout.LayoutParams(Ui.Match, Ui.Wrap));
        root.AddView(divider);

        var holder = new ModViewHolder(root, name, meta, badge, badgeHolder, toggle, text, divider, onToggled);
        Ui.MakeTappable(row, () =>
        {
            if (holder._mod != null)
                onOpen(holder._mod);
        });
        badgeHolder.Click += (_, _) =>
        {
            if (holder._mod != null)
                CompatUi.ShowTip(holder._badge, holder._mod);
        };
        return holder;
    }

    ModViewHolder(View root, TextView name, TextView meta, TextView badge, LinearLayout badgeHolder,
        SwitchCompat toggle, LinearLayout text, View divider, Action<ModInfo> onToggled)
        : base(root)
    {
        _name = name;
        _meta = meta;
        _badge = badge;
        _badgeHolder = badgeHolder;
        _toggle = toggle;
        _text = text;
        _divider = divider;
        _onToggled = onToggled;

        _toggle.CheckedChange += (_, e) =>
        {
            if (_binding || _mod == null) return;
            _mod.Enabled = e.IsChecked;
            _onToggled(_mod);

            // the rename can fail, so always show what is really on disk
            _binding = true;
            _toggle.Checked = _mod.Enabled;
            _binding = false;
            _text.Animate().Alpha(_mod.Enabled ? 1f : 0.5f).SetDuration(160).Start();
        };
    }

    public void Bind(ModInfo mod, bool isLast)
    {
        _mod = mod;
        _binding = true;
        _toggle.Checked = mod.Enabled;
        _binding = false;

        _name.Text = mod.Name;
        _divider.Visibility = isLast ? ViewStates.Gone : ViewStates.Visible;
        _text.Alpha = mod.Enabled ? 1f : 0.5f;

        if (mod.Compat != null)
        {
            _badgeHolder.Visibility = ViewStates.Visible;
            CompatUi.Apply(_badge, mod.Compat);
        }
        else
        {
            _badgeHolder.Visibility = ViewStates.Gone;
        }

        var parts = new[] { mod.Author, mod.Version.Length > 0 ? mod.Version : "" };
        var meta = string.Join(" · ", parts.Where(p => p.Length > 0));
        _meta.Text = meta.Length > 0 ? meta : Path.GetFileName(mod.ModFolderPath ?? "").TrimStart('.');
    }
}
