using Android.OS;
using System;
using System.IO;
using System.Linq;
using Xamarin.Essentials;

namespace SMAPIGameLoader.Launcher;

// When each mod was added, so the list can be sorted by it. The launcher notes the time at install.
// Mods that were already there fall back to their folder's own timestamp.
internal static class ModStamps
{
    // a mod is the same mod whether or not its folder is switched off (a leading dot)
    static string Key(string relative)
    {
        var parts = (relative ?? "")
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.TrimStart('.'));
        return "added:" + string.Join("/", parts);
    }

    static string Relative(string folderPath)
    {
        var path = folderPath ?? "";
        int at = path.IndexOf("/Mods/", StringComparison.Ordinal);
        return at >= 0 ? path.Substring(at + 6) : Path.GetFileName(path);
    }

    public static void Record(string relativeFolder)
    {
        if (!string.IsNullOrWhiteSpace(relativeFolder))
            Preferences.Set(Key(relativeFolder), DateTime.UtcNow);
    }

    public static DateTime For(ModInfo mod)
    {
        var saved = Preferences.Get(Key(Relative(mod.ModFolderPath)), DateTime.MinValue);
        if (saved != DateTime.MinValue)
            return saved;

        try { return Directory.GetLastWriteTimeUtc(mod.ModFolderPath); }
        catch { return DateTime.MinValue; }
    }
}
