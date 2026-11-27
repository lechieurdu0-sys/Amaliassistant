using System;
using System.IO;
using System.Collections.Generic;

namespace GameOverlay.Kikimeter.Services;

// A deletion suppresses only evidence already present in this file generation.
public sealed class CharacterPresenceCursor
{
    private readonly LogTailCursor _cursor = new();
    private readonly Dictionary<string, long> _cutoffs = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _creation;
    private bool _opened;
    private bool _missing;

    public void InitializeAtEnd(string path)
    {
        _cursor.Reset();
        _cutoffs.Clear();
        _opened = _missing = false;
        try
        {
            using var stream = Open(path);
            _cursor.InitializeAtEnd(stream);
            _creation = File.GetCreationTimeUtc(path);
            _opened = true;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (ArgumentException) { }
    }

    public void Forget(string path, string name)
    {
        try
        {
            if (File.Exists(path))
                _cutoffs[name] = new FileInfo(path).Length;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public bool IsFresh(string name) =>
        !_cutoffs.TryGetValue(name, out long cutoff) || _cursor.LastLineStart >= cutoff;

    public void Read(string path, Action<string> onLine)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _missing = true;
            return;
        }
        using var stream = Open(path);
        var creation = File.GetCreationTimeUtc(path);
        bool replaced = _opened && (_missing || _creation != creation);
        _opened = true;
        _missing = false;
        _creation = creation;
        _cursor.Read(stream, onLine, () => _cutoffs.Clear(), replaced);
    }

    private static FileStream Open(string path) => new(path, FileMode.Open,
        FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
}
