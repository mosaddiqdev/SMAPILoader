using Android.Graphics;
using System;
using System.IO;
using System.Linq;
using System.Xml;

namespace SMAPIGameLoader.Launcher;

internal readonly record struct FarmDate(string Season, int Day, int Year);

// Reads the farm's own date from the newest save. SaveGameInfo is the small summary file the
// game's load menu uses, and it carries the season, day and year of that save.
internal static class FarmReader
{
    public static FarmDate? Latest()
    {
        try
        {
            var saves = SaveManager.GetSavesDir();
            if (!Directory.Exists(saves))
                return null;

            var newest = Directory.GetDirectories(saves)
                .Select(dir => new FileInfo(Path.Combine(dir, "SaveGameInfo")))
                .Where(file => file.Exists)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            return newest == null ? null : Read(newest.FullName);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return null;
        }
    }

    static FarmDate? Read(string path)
    {
        string rawSeason = null;
        int day = 0, year = 0;
        bool hasDay = false, hasYear = false;

        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, IgnoreWhitespace = true };
        using var reader = XmlReader.Create(path, settings);

        while (!reader.EOF && !(rawSeason != null && hasDay && hasYear))
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                switch (reader.Name)
                {
                    case "seasonForSaveGame":
                        rawSeason = reader.ReadElementContentAsString();
                        continue;
                    case "dayOfMonthForSaveGame":
                        hasDay = int.TryParse(reader.ReadElementContentAsString(), out day);
                        continue;
                    case "yearForSaveGame":
                        hasYear = int.TryParse(reader.ReadElementContentAsString(), out year);
                        continue;
                }
            }
            reader.Read();
        }

        var season = SeasonName(rawSeason);
        if (season == null || !hasDay || !hasYear)
            return null;

        return new FarmDate(season, day, year);
    }

    // the game stores the season as a number (0 to 3), older saves may spell it out
    static string SeasonName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (int.TryParse(raw, out int index))
        {
            return index switch { 0 => "Spring", 1 => "Summer", 2 => "Fall", 3 => "Winter", _ => null };
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "spring" => "Spring",
            "summer" => "Summer",
            "fall" => "Fall",
            "winter" => "Winter",
            _ => null,
        };
    }
}

// The season Home shows: the farm's own when there is a save, otherwise the phone's calendar.
internal static class Season
{
    public static FarmDate? Farm { get; private set; }

    public static string Name => Farm?.Season ?? Tokens.SeasonName;

    public static Color Color => Name switch
    {
        "Spring" => Android.Graphics.Color.ParseColor("#8FB07A"),
        "Summer" => Android.Graphics.Color.ParseColor("#D4AE5A"),
        "Fall" => Android.Graphics.Color.ParseColor("#C98A52"),
        _ => Android.Graphics.Color.ParseColor("#8FA7B8"),
    };

    public static void Refresh() => Farm = FarmReader.Latest();
}
