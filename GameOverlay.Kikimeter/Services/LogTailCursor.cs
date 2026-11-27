using System;
using System.IO;
using System.Text;

namespace GameOverlay.Kikimeter.Services;

// Byte-oriented cursor: UTF-8 and incomplete lines survive across polling ticks.
public sealed class LogTailCursor
{
    private const int AnchorSize = 64;
    private const int MaxLineBytes = 1024 * 1024;
    private readonly MemoryStream _pending = new();
    private byte[] _anchor = Array.Empty<byte>();
    private long _anchorOffset;
    private bool _discardLine;
    public long Position { get; private set; }

    public void InitializeAtEnd(Stream stream)
    {
        Reset();
        Position = stream.Length;
        if (Position > 0)
        {
            stream.Position = Position - 1;
            _discardLine = stream.ReadByte() != '\n';
        }
        CaptureAnchor(stream);
    }

    public void Reset()
    {
        Position = 0;
        _pending.SetLength(0);
        _anchor = Array.Empty<byte>();
        _anchorOffset = 0;
        _discardLine = false;
    }

    public void Read(Stream stream, Action<string> onLine, Action onRotation, bool replaced = false)
    {
        if (replaced || stream.Length < Position || !AnchorMatches(stream))
        {
            Reset();
            onRotation();
        }

        // Read a finite snapshot. Later appends are processed on the next tick.
        long limit = Math.Min(stream.Length, Position + 1024 * 1024);
        stream.Position = Position;
        var buffer = new byte[16 * 1024];
        while (Position < limit)
        {
            int count = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, limit - Position));
            if (count == 0) break;
            for (int i = 0; i < count; i++)
            {
                byte value = buffer[i];
                Position++;
                if (value == '\n')
                {
                    string? line = null;
                    if (!_discardLine)
                        line = Encoding.UTF8.GetString(_pending.GetBuffer(), 0, (int)_pending.Length)
                            .TrimEnd('\r').TrimStart('\uFEFF');
                    _pending.SetLength(0);
                    _discardLine = false;
                    if (line != null) onLine(line);
                }
                else if (!_discardLine)
                {
                    if (_pending.Length >= MaxLineBytes)
                    {
                        _pending.SetLength(0);
                        _discardLine = true; // Protect against a corrupt/unbounded line.
                    }
                    else _pending.WriteByte(value);
                }
            }
        }
        CaptureAnchor(stream);
    }

    private bool AnchorMatches(Stream stream)
    {
        if (_anchor.Length == 0) return true;
        if (stream.Length < _anchorOffset + _anchor.Length) return false;
        stream.Position = _anchorOffset;
        var actual = new byte[_anchor.Length];
        int count = stream.ReadAtLeast(actual, actual.Length, false);
        return count == actual.Length && actual.AsSpan().SequenceEqual(_anchor);
    }

    private void CaptureAnchor(Stream stream)
    {
        int count = (int)Math.Min(Position, AnchorSize);
        long offset = Position - count;
        stream.Position = offset;
        var bytes = new byte[count];
        if (stream.ReadAtLeast(bytes, count, false) == count)
        {
            _anchor = bytes;
            _anchorOffset = offset;
        }
    }
}
