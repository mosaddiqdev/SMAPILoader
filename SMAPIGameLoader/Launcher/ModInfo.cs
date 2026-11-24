using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SMAPIGameLoader.Launcher;

public record ModDependency(string Id, bool Required);

public class ModInfo
{
    public string Name { get; private set; } = "";
    public string Version { get; private set; } = "";
    public string Author { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string UniqueId { get; private set; } = "";
    public List<ModDependency> Dependencies { get; } = new();

    // Filled in by ModLibrary.Resolve for mods that are turned on.
    public List<string> MissingDependencies { get; set; } = new();

    // Set when manifest.json can't be read. The row shows it instead of popping a dialog per mod.
    public string LoadError { get; private set; }

    public string ModFolderPath => modFolderPath;
    string modFolderPath;
    string modManifestPath;

    public bool Enabled
    {
        get => IsEnable();
        set => ToggleMod(value);
    }

    public void ToggleMod(bool setNewActive)
    {
        if (setNewActive == Enabled) return;

        Console.WriteLine($"try toggle mod to: {setNewActive} for mod: {modFolderPath}");
        try
        {
            var oldPath = modFolderPath;
            var parent = Path.GetDirectoryName(oldPath)!;
            var folderName = Path.GetFileName(oldPath);

            // strip a single leading dot so a real name that starts with "." is not damaged
            var baseName = folderName.StartsWith(".") ? folderName.Substring(1) : folderName;
            var newName = setNewActive ? baseName : "." + baseName;
            var newPath = Path.Combine(parent, newName);

            if (Directory.Exists(newPath))
                throw new IOException($"Target already exists: {newPath}");

            Directory.Move(oldPath, newPath);
            UpdatePaths(newPath);
        }
        catch (Exception ex)
        {
            ErrorDialogTool.Show(ex, "Error try set mod active: " + modFolderPath);
        }
    }

    void UpdatePaths(string newFolderPath)
    {
        modFolderPath = newFolderPath;
        modManifestPath = Path.Combine(newFolderPath, "manifest.json");
    }

    bool IsEnable()
    {
        var folderName = Path.GetFileName(modFolderPath);
        return !string.IsNullOrEmpty(folderName) && !folderName.StartsWith('.');
    }

    public ModInfo(string manifestFilePath)
    {
        modManifestPath = manifestFilePath;
        modFolderPath = Path.GetDirectoryName(manifestFilePath);
        Name = Path.GetFileName(modFolderPath).TrimStart('.');

        try
        {
            var manifest = JObject.Parse(File.ReadAllText(manifestFilePath));

            Name = Read(manifest, "Name", Name);
            Version = Read(manifest, "Version", "");
            Author = Read(manifest, "Author", "");
            Description = Read(manifest, "Description", "");
            UniqueId = Read(manifest, "UniqueID", "");

            if (manifest["Dependencies"] is JArray deps)
            {
                foreach (var dep in deps)
                {
                    var id = Read(dep as JObject, "UniqueID", "");
                    if (id.Length == 0) continue;
                    bool required = dep["IsRequired"]?.Value<bool?>() ?? true;
                    Dependencies.Add(new ModDependency(id, required));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            LoadError = "Can’t read manifest.json";
        }
    }

    static string Read(JObject json, string key, string fallback)
    {
        var value = json?[key]?.ToString();
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}

internal static class ModLibrary
{
    public static List<ModInfo> Scan()
    {
        var mods = new List<ModInfo>();
        try
        {
            var manifests = new List<string>();
            ModTool.FindManifestFile(ModTool.ModsDir, manifests);
            foreach (var path in manifests)
                mods.Add(new ModInfo(path));
        }
        catch (Exception ex)
        {
            ErrorDialogTool.Show(ex, "Error try to read the Mods folder");
        }

        Resolve(mods);
        return mods;
    }

    // A mod that is on but needs something that is off or missing gets flagged.
    public static void Resolve(List<ModInfo> mods)
    {
        var active = new HashSet<string>(
            mods.Where(m => m.Enabled && m.UniqueId.Length > 0).Select(m => m.UniqueId),
            StringComparer.OrdinalIgnoreCase);

        foreach (var mod in mods)
        {
            mod.MissingDependencies = mod.Enabled
                ? mod.Dependencies.Where(d => d.Required && !active.Contains(d.Id)).Select(d => d.Id).ToList()
                : new List<string>();
        }
    }
}
