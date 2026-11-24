using Android.Content;
using Android.Views;
using AndroidX.RecyclerView.Widget;
using System;
using System.Collections.Generic;

namespace SMAPIGameLoader.Launcher;

internal class ModAdapter : RecyclerView.Adapter
{
    readonly Context _context;
    readonly Action<ModInfo> _onToggled;
    readonly Action<ModInfo> _onOpen;
    List<ModInfo> _mods = new();

    public ModAdapter(Context context, Action<ModInfo> onToggled, Action<ModInfo> onOpen)
    {
        _context = context;
        _onToggled = onToggled;
        _onOpen = onOpen;
    }

    public void Replace(List<ModInfo> mods)
    {
        _mods = mods;
        NotifyDataSetChanged();
    }

    public override int ItemCount => _mods.Count;

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        return ModViewHolder.Create(_context, _onToggled, _onOpen);
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        ((ModViewHolder)holder).Bind(_mods[position], position == _mods.Count - 1);
    }
}
