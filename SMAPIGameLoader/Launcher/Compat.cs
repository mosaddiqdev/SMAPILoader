using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SMAPIGameLoader.Launcher;

public enum CompatLevel { Compatible, Incompatible }

// What we could verify about a mod from its manifest and your setup.
public sealed class ModCompat
{
    public CompatLevel Level { get; init; }
    public List<string> Problems { get; init; } = new();
    public List<string> Passed { get; init; } = new();
}

// Manifest versions look like "4.0.0", "1.6.15" or "2.1.0-beta.3". Only the numbers matter here.
public static class VersionText
{
    static readonly Regex Numbers = new(@"^\s*v?(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:\.(\d+))?", RegexOptions.Compiled);

    public static Version Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = Numbers.Match(text);
        if (!match.Success)
            return null;

        int Part(int index) => match.Groups[index].Success ? int.Parse(match.Groups[index].Value) : 0;
        return new Version(Part(1), Part(2), Part(3), Part(4));
    }
}
