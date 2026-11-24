using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SMAPIGameLoader.Launcher;

// Alert sits between Info and Warn so the Info filter keeps it and the Warn filter drops it.
public enum LogLevel { Trace, Debug, Info, Alert, Warn, Error }

public sealed class LogEntry
{
    public string Raw { get; init; }
    public LogLevel Level { get; init; }
    public int LevelStart { get; init; }
    public int LevelEnd { get; init; }
    public int HeaderLength { get; init; }
}

// Reads SMAPI's own log file. Used live while the game runs and whole after it has closed.
public sealed class LogSession
{
    public const int MaxLines = 3000;
    const int ChunkBytes = 256 * 1024;

    static readonly Regex Header = new(
        @"^\[(\d{2}:\d{2}:\d{2}) (TRACE|DEBUG|INFO|WARN|ERROR|ALERT)\s+[^\]]*\]",
        RegexOptions.Compiled);

    readonly List<LogEntry> _lines = new();
    readonly DateTime _notBeforeUtc;
    long _position;
    LogLevel _lastLevel = LogLevel.Info;

    public static string LogPath => Path.Combine(FileTool.ExternalFilesDir, "ErrorLogs", "SMAPI-latest.txt");

    public IReadOnlyList<LogEntry> Lines => _lines;
    public int Warnings { get; private set; }
    public int Errors { get; private set; }

    // A live session ignores the file until it has been written after the game started,
    // so last run's log never shows up as this run's.
    public LogSession(DateTime? notBeforeUtc = null)
    {
        _notBeforeUtc = notBeforeUtc ?? DateTime.MinValue;
    }

    public bool Poll()
    {
        var file = new FileInfo(LogPath);
        if (!file.Exists || file.LastWriteTimeUtc < _notBeforeUtc)
            return false;

        bool changed = false;
        if (file.Length < _position)
        {
            Clear();
            changed = true;
        }
        if (file.Length == _position)
            return changed;

        using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        stream.Seek(_position, SeekOrigin.Begin);

        var buffer = new byte[(int)Math.Min(file.Length - _position, ChunkBytes)];
        int read = stream.Read(buffer, 0, buffer.Length);
        if (read <= 0)
            return changed;

        // only take whole lines, so a half-written line or split character is picked up next time
        int lastNewline = Array.LastIndexOf<byte>(buffer, (byte)'\n', read - 1);
        if (lastNewline < 0)
        {
            if (read < buffer.Length)
                return changed;
            lastNewline = read - 1;
        }

        int used = lastNewline + 1;
        _position += used;
        Append(Encoding.UTF8.GetString(buffer, 0, used));
        return true;
    }

    public void LoadAll()
    {
        for (int i = 0; i < 40 && Poll(); i++) { }
    }

    void Clear()
    {
        _position = 0;
        _lines.Clear();
        _lastLevel = LogLevel.Info;
        Warnings = 0;
        Errors = 0;
    }

    void Append(string text)
    {
        foreach (var piece in text.Split('\n'))
        {
            var line = piece.TrimEnd('\r');
            if (line.Length == 0)
                continue;

            var match = Header.Match(line);
            if (match.Success)
            {
                _lastLevel = Enum.Parse<LogLevel>(match.Groups[2].Value, true);
                if (_lastLevel == LogLevel.Warn) Warnings++;
                else if (_lastLevel == LogLevel.Error) Errors++;

                var level = match.Groups[2];
                _lines.Add(new LogEntry
                {
                    Raw = line,
                    Level = _lastLevel,
                    LevelStart = level.Index,
                    LevelEnd = level.Index + level.Length,
                    HeaderLength = match.Length
                });
            }
            else
            {
                // stack traces and wrapped messages carry the level of the line above
                _lines.Add(new LogEntry { Raw = line, Level = _lastLevel });
            }
        }

        if (_lines.Count > MaxLines)
            _lines.RemoveRange(0, _lines.Count - MaxLines);
    }

    public static string ToText(IEnumerable<LogEntry> entries)
    {
        return string.Join("\n", entries.Select(e => e.Raw));
    }
}
