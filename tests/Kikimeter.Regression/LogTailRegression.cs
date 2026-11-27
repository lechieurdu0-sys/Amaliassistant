using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using GameOverlay.Kikimeter.Services;

internal static class LogTailRegression
{
    public static void Run()
    {
        using var stream = new MemoryStream();
        var cursor = new LogTailCursor();
        var lines = new List<string>();
        int rotations = 0;
        void Append(byte[] bytes) { stream.Position = stream.Length; stream.Write(bytes); }
        void Add(string text) => Append(Encoding.UTF8.GetBytes(text));
        void Poll(bool replaced = false) => cursor.Read(stream, lines.Add, () => rotations++, replaced);
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        Add("history\n");
        cursor.InitializeAtEnd(stream);
        Add("Alpha: -");
        Poll();
        Check(lines.Count == 0, "Partial line emitted");
        Add("42 PV\r\n");
        Poll();
        Poll();
        Check(lines.Count == 1 && lines[0] == "Alpha: -42 PV", "Partial line lost or duplicated");

        var utf8 = Encoding.UTF8.GetBytes("Enflammé\n");
        Append(utf8[..^2]);
        Poll();
        Append(utf8[^2..]);
        Poll();
        Check(lines.Count == 2 && lines[1] == "Enflammé", "Split UTF-8 character corrupted");

        Add("unfinished");
        Poll();
        stream.SetLength(0);
        Add("new\n");
        Poll();
        Check(rotations == 1 && lines.Count == 3 && lines[2] == "new", "Truncation dropped new file data");

        stream.SetLength(0);
        Add("replacement longer than the previous file\n");
        Poll();
        Check(rotations == 2 && lines.Count == 4, "Longer replacement not detected");

        stream.SetLength(0);
        Add("first\n");
        Poll(true);
        Check(rotations == 3 && lines[^1] == "first", "Explicit replacement ignored");

        stream.SetLength(0);
        Add("old partial");
        cursor.InitializeAtEnd(stream);
        Add(" historical remainder\nreal\n");
        Poll();
        Check(lines.Count == 6 && lines[^1] == "real", "Old partial history was processed");
    }
}
