using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SMAPIGameLoader.Launcher;

public record ModDependency(string Id, bool Required, string MinVersion = "");

public class ModInfo
{
    public string Name { get; private set; } = "";
    public string Version { get; private set; } = "";
    public string Author { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string UniqueId { get; private set; } = "";
    public string MinimumApiVersion { get; private set; } = "";
    public string MinimumGameVersion { get; private set; } = "";
    public string EntryDll { get; private set; } = "";
    public List<ModDependency> Dependencies { get; } = new();

    // Filled in by ModLibrary.Resolve.
    public ModCompat Compat { get; set; }

    // Set when manifest.json can't be read. The badge explains it instead of a dialog per mod.
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
            MinimumApiVersion = Read(manifest, "MinimumApiVersion", "");
            MinimumGameVersion = Read(manifest, "MinimumGameVersion", "");
            EntryDll = Read(manifest, "EntryDll", "");

            if (manifest["Dependencies"] is JArray deps)
            {
                foreach (var dep in deps)
                {
                    var id = Read(dep as JObject, "UniqueID", "");
                    if (id.Length == 0) continue;
                    bool required = dep["IsRequired"]?.Value<bool?>() ?? true;
                    Dependencies.Add(new ModDependency(id, required, Read(dep as JObject, "MinimumVersion", "")));
                }
            }

            // a content pack always needs the mod it was written for
            if (manifest["ContentPackFor"] is JObject pack)
            {
                var id = Read(pack, "UniqueID", "");
                if (id.Length > 0 && !Dependencies.Any(d => d.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
                    Dependencies.Add(new ModDependency(id, true, Read(pack, "MinimumVersion", "")));
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

    // Works out compatibility for every mod against your SMAPI, your game and the other installed mods.
    public static void Resolve(List<ModInfo> mods)
    {
        Version smapi = null;
        try
        {
            if (SMAPIInstaller.IsInstalled)
                smapi = SMAPIInstaller.GetCurrentVersion();
        }
        catch { }

        Version game = null;
        try
        {
            game = StardewApkTool.CurrentGameVersion;
            if (game == null || game.Major == 0)
                game = null;
        }
        catch { }

        var byId = new Dictionary<string, List<ModInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods.Where(m => m.UniqueId.Length > 0))
        {
            if (!byId.TryGetValue(mod.UniqueId, out var list))
                byId[mod.UniqueId] = list = new List<ModInfo>();
            list.Add(mod);
        }

        foreach (var mod in mods)
            mod.Compat = Check(mod, byId, smapi, game);
    }

    static ModCompat Check(ModInfo mod, Dictionary<string, List<ModInfo>> byId, Version smapi, Version game)
    {
        var problems = new List<string>();
        var passed = new List<string>();

        if (mod.LoadError != null)
        {
            problems.Add("manifest.json can’t be read. The file may be damaged or have a typo.");
            return Result(problems, passed);
        }

        if (mod.Enabled && mod.UniqueId.Length > 0 && byId[mod.UniqueId].Any(other => other != mod && other.Enabled))
            problems.Add("Another copy of this mod is turned on. Remove or turn off the extra one.");

        var minApi = VersionText.Parse(mod.MinimumApiVersion);
        if (minApi != null && smapi != null)
        {
            if (smapi < minApi)
                problems.Add($"Needs SMAPI {mod.MinimumApiVersion} or newer. You have {smapi}.");
            else
                passed.Add($"SMAPI {smapi} meets the required {mod.MinimumApiVersion}.");
        }

        var minGame = VersionText.Parse(mod.MinimumGameVersion);
        if (minGame != null && game != null)
        {
            if (game < minGame)
                problems.Add($"Needs Stardew Valley {mod.MinimumGameVersion} or newer. You have {game}.");
            else
                passed.Add($"Game {game} meets the required {mod.MinimumGameVersion}.");
        }

        if (mod.EntryDll.Length > 0)
            CheckEntryDll(mod, problems, passed);

        foreach (var dep in mod.Dependencies)
        {
            if (!dep.Required)
                continue;

            if (!byId.TryGetValue(dep.Id, out var found))
            {
                problems.Add($"Needs {dep.Id}, which isn’t installed.");
                continue;
            }

            var target = found.FirstOrDefault(m => m.Enabled) ?? found[0];
            var min = VersionText.Parse(dep.MinVersion);
            var have = VersionText.Parse(target.Version);

            if (mod.Enabled && !target.Enabled)
                problems.Add($"{target.Name} is installed but turned off. Turn it on.");
            else if (min != null && have != null && have < min)
                problems.Add($"Needs {target.Name} {dep.MinVersion} or newer. You have {target.Version}.");
            else
                passed.Add($"{target.Name} is installed.");
        }

        return Result(problems, passed);
    }

    // File names are case-sensitive on Android, and plenty of mods were only ever tested on Windows.
    static void CheckEntryDll(ModInfo mod, List<string> problems, List<string> passed)
    {
        try
        {
            if (File.Exists(Path.Combine(mod.ModFolderPath, mod.EntryDll)))
            {
                passed.Add($"{mod.EntryDll} is in the mod folder.");
                return;
            }

            var actual = Directory.GetFiles(mod.ModFolderPath)
                .Select(Path.GetFileName)
                .FirstOrDefault(f => string.Equals(f, mod.EntryDll, StringComparison.OrdinalIgnoreCase));

            if (actual != null)
                problems.Add($"The manifest says {mod.EntryDll} but the file is {actual}. Android treats capital letters as different.");
            else
                problems.Add($"{mod.EntryDll} is missing from the mod folder. Reinstall the mod.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    static ModCompat Result(List<string> problems, List<string> passed)
    {
        if (problems.Count == 0 && passed.Count == 0)
            passed.Add("Nothing in its manifest conflicts with your setup.");

        return new ModCompat
        {
            Level = problems.Count == 0 ? CompatLevel.Compatible : CompatLevel.Incompatible,
            Problems = problems,
            Passed = passed
        };
    }
}
