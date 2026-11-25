using Android;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace SMAPIGameLoader;

internal static class FilePickerTool
{
    // some file managers report zips as x-zip-compressed
    public static FilePickerFileType FileTypeZip = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.Android, new[] { "application/zip", "application/x-zip-compressed" } },
    });

    static PickOptions ZipOptions(string title) => new PickOptions
    {
        PickerTitle = title ?? "Please select zip file",
        FileTypes = FileTypeZip,
    };

    // android 6 --> 10 need to request External read write storage
    static bool HasStoragePermission()
    {
        var activity = SMAPIGameLoader.Launcher.LauncherActivity.Instance;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M && Build.VERSION.SdkInt <= BuildVersionCodes.Q)
        {
            if (ContextCompat.CheckSelfPermission(activity, Manifest.Permission.ReadExternalStorage) != Permission.Granted
                || ContextCompat.CheckSelfPermission(activity, Manifest.Permission.WriteExternalStorage) != Permission.Granted)
            {
                ToastNotifyTool.Notify("Please Click Allow File Access Permission");
                ActivityCompat.RequestPermissions(activity,
                    [Manifest.Permission.ReadExternalStorage, Manifest.Permission.WriteExternalStorage],
                    1000);
                return false;
            }
        }
        return true;
    }

    public static async Task<FileResult> PickZipFile(string title = null)
    {
        if (!HasStoragePermission())
            return null;

        return await FilePicker.PickAsync(ZipOptions(title));
    }

    // Same picker, but you can tick as many zips as you like.
    public static async Task<IEnumerable<FileResult>> PickZipFiles(string title = null)
    {
        if (!HasStoragePermission())
            return null;

        return await FilePicker.PickMultipleAsync(ZipOptions(title ?? "Select mod zips"));
    }
}
