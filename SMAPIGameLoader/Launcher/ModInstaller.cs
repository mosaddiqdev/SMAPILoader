using Newtonsoft.Json.Linq;
using SMAPIGameLoader.Tool;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace SMAPIGameLoader.Launcher;

internal static class ModInstaller
{
    public static string ModsDirPath = Path.Combine(FileTool.ExternalFilesDir, "Mods");
    public static Version GetMinGameVersion(JObject manifest)
    {
        try
        {
            return new Version(manifest["MinimumGameVersion"].ToString());
        }
        catch
        {
            return null;
        }
    }
    public static Version GetMinSMAPIVersion(JObject manifest)
    {
        try
        {
            return new Version(manifest["MinimumApiVersion"].ToString());
        }
        catch
        {
            return null;
        }
    }
    public static bool AssertModISupport(JObject manifest)
    {
        if (SMAPIInstaller.IsInstalled is false)
        {
            ToastNotifyTool.Notify("Can't check mod, please install SMAPI first!!");
            return false;
        }

        //must be 1.6++
        var minGameVersion = GetMinGameVersion(manifest);
        if (minGameVersion != null && minGameVersion < new Version(1, 6, 0))
        {
            ToastNotifyTool.Notify("Not support for game version 1.6");
            return false;
        }

        //check smapi must be 4.0.00++
        var minSMAPIVersion = GetMinSMAPIVersion(manifest);
        if (minSMAPIVersion != null & minSMAPIVersion < new Version(4, 0, 0))
        {
            ToastNotifyTool.Notify("Not support for game version 1.6");
            return false;
        }

        bool isContentPack = manifest.ContainsKey("ContentPackFor");
        return true;
    }

    sealed record InstallResult(string File, List<string> Mods, string Error);

    // Pick any number of zips, install them one after another, then show a single summary.
    public static async void OnClickInstallMod(Action OnInstalledCallback = null)
    {
        try
        {
            var picks = (await FilePickerTool.PickZipFiles())?.ToList();
            if (picks == null || picks.Count == 0)
                return;

            TaskTool.Run(LauncherActivity.Instance, () =>
            {
                TaskTool.SetTitle(picks.Count == 1 ? "Installing mod" : $"Installing {picks.Count} mods");

                var results = new List<InstallResult>();
                for (int i = 0; i < picks.Count; i++)
                {
                    TaskTool.NewLine($"{i + 1}/{picks.Count}  {picks[i].FileName}");
                    results.Add(InstallOne(picks[i]));
                }

                TaskTool.CloseBusyDialog();
                ShowSummary(results);
                MainThread.BeginInvokeOnMainThread(() => OnInstalledCallback?.Invoke());
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            ErrorDialogTool.Show(ex);
        }
    }

    // One zip can hold one mod or a whole pack. A bad zip is skipped, it never stops the rest.
    static InstallResult InstallOne(FileResult pick)
    {
        try
        {
            using var zip = ZipFile.OpenRead(pick.FullPath);
            var manifests = zip.Entries.Where(entry => entry.Name == ModTool.ManifiestFileName).ToArray();
            if (manifests.Length == 0)
                return new InstallResult(pick.FileName, new List<string>(), "no manifest.json inside, so it isn’t a mod");

            var names = new List<string>();
            foreach (var entry in manifests)
            {
                try
                {
                    var json = JObject.Parse(ReadManifest(entry));
                    var name = json["Name"]?.ToString();
                    var version = json["Version"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name))
                        names.Add(entry.FullName);
                    else
                        names.Add(string.IsNullOrWhiteSpace(version) ? name : $"{name} {version}");
                }
                catch
                {
                    names.Add(entry.FullName);
                }
            }

            ExtractModZipFile(pick.FileName, zip, ModsDirPath);

            // note when each mod arrived, so the list can be sorted by it
            foreach (var entry in manifests)
                ModStamps.Record(Path.GetDirectoryName(entry.FullName));

            return new InstallResult(pick.FileName, names, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return new InstallResult(pick.FileName, new List<string>(), ex.Message);
        }
    }

    static void ShowSummary(List<InstallResult> results)
    {
        var added = results.Where(r => r.Error == null).SelectMany(r => r.Mods).ToList();
        var skipped = results.Where(r => r.Error != null).ToList();

        var text = new StringBuilder();
        if (added.Count > 0)
        {
            text.AppendLine("Added");
            foreach (var name in added)
                text.AppendLine("  " + name);
        }
        if (skipped.Count > 0)
        {
            if (text.Length > 0)
                text.AppendLine();
            text.AppendLine("Skipped");
            foreach (var result in skipped)
                text.AppendLine($"  {result.File}: {result.Error}");
        }

        string title;
        if (skipped.Count == 0)
            title = added.Count == 1 ? "Installed 1 mod" : $"Installed {added.Count} mods";
        else if (added.Count == 0)
            title = "Nothing installed";
        else
            title = $"Installed {added.Count}, skipped {skipped.Count}";

        DialogTool.Show(title, text.ToString().TrimEnd());
    }

    public static void ExtractModZipFile(string zipFilePath, ZipArchive zip, string outputDir)
    {
        //fix bug
        //if you have file with name == zip.fileNameWithoutExtension
        //example exist file "SpaceCore" 
        var fileNameNoExtens = new FileInfo(zipFilePath).Name.Replace(".zip", "");
        var checkFileExist = Path.Combine(outputDir, fileNameNoExtens);
        if (File.Exists(checkFileExist))
            File.Delete(checkFileExist);

        zip.ExtractToDirectory(outputDir, true);
    }
    public static string ReadManifest(ZipArchiveEntry entry)
    {
        string result;
        using (StreamReader reader = new StreamReader(entry.Open()))
        {
            result = reader.ReadToEnd();
        }
        return result;
    }
    internal static bool TryDeleteMod(string folderPath, bool cleaupParentFolder = false)
    {
        try
        {
            // check if is root Mods
            if (ModsDirPath == folderPath)
                return false;

            if (Directory.Exists(folderPath) is false)
                return false;

            Directory.Delete(folderPath, true);

            //clean up folder parent if need
            if (cleaupParentFolder)
            {
                var parentDir = Directory.GetParent(folderPath).FullName;
                if (parentDir != ModsDirPath)
                {
                    var dirs = Directory.GetDirectories(parentDir);
                    if (dirs.Length == 0)
                        Directory.Delete(parentDir, true);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            ErrorDialogTool.Show(ex);
            return true;
        }
    }
}
