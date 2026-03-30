using System.Text;

namespace Final_Test_Hybrid.Components.Logs;

internal sealed class LogFileTailReader(int maxLines)
{
    private const int InitialTailBytes = 4 * 1024 * 1024;

    private readonly Queue<string> _lines = new();
    private string? _currentPath;
    private long _position;
    private DateTime _creationTimeUtc;
    private string _pendingLine = string.Empty;

    public LogTailSnapshot Refresh(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            return !StringComparer.OrdinalIgnoreCase.Equals(path, _currentPath)
                ? LoadTail(path)
                : Continue(path);
        }

        Reset(path);
        return Build("Файл лога не найден");
    }

    private LogTailSnapshot LoadTail(string path)
    {
        Reset(path);
        using var stream = OpenRead(path);
        _creationTimeUtc = File.GetCreationTimeUtc(path);
        if (stream.Length == 0)
        {
            return Build(null);
        }

        var offset = Math.Max(0, stream.Length - InitialTailBytes);
        var text = ReadText(stream, offset, stream.Length - offset);
        _position = stream.Length;
        ProcessChunk(text, RequiresLeadingTrim(stream, offset));
        return Build(null);
    }

    private LogTailSnapshot Continue(string path)
    {
        using var stream = OpenRead(path);
        var creationTimeUtc = File.GetCreationTimeUtc(path);
        if (creationTimeUtc != _creationTimeUtc
            || stream.Length < _position
            || stream.Length - _position > InitialTailBytes)
        {
            return LoadTail(path);
        }

        if (stream.Length == _position)
        {
            return Build(null);
        }

        var text = ReadText(stream, _position, stream.Length - _position);
        _position = stream.Length;
        _creationTimeUtc = creationTimeUtc;
        ProcessChunk(text, false);
        return Build(null);
    }

    private void ProcessChunk(string text, bool dropFirstPartialLine)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var normalized = NormalizeNewLines(text);
        if (dropFirstPartialLine)
        {
            normalized = SkipLeadingPartialLine(normalized);
        }

        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        if (_pendingLine.Length > 0)
        {
            normalized = _pendingLine + normalized;
            _pendingLine = string.Empty;
        }

        var parts = normalized.Split('\n');
        var completeCount = parts.Length;
        if (!normalized.EndsWith('\n'))
        {
            _pendingLine = parts[^1];
            completeCount--;
        }

        for (var i = 0; i < completeCount; i++)
        {
            if (parts[i].Length == 0)
            {
                continue;
            }

            AddLine(parts[i]);
        }
    }

    private void AddLine(string line)
    {
        _lines.Enqueue(line);
        while (_lines.Count > maxLines)
        {
            _lines.Dequeue();
        }
    }

    private void Reset(string? path)
    {
        _currentPath = path;
        _position = 0;
        _creationTimeUtc = default;
        _pendingLine = string.Empty;
        _lines.Clear();
    }

    private LogTailSnapshot Build(string? message)
    {
        var snapshotLines = _lines.ToList();
        if (_pendingLine.Length > 0)
        {
            snapshotLines.Add(_pendingLine);
        }

        if (snapshotLines.Count > maxLines)
        {
            snapshotLines = snapshotLines.TakeLast(maxLines).ToList();
        }

        return new LogTailSnapshot(snapshotLines, message);
    }

    private static FileStream OpenRead(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    }

    private static string ReadText(FileStream stream, long offset, long count)
    {
        stream.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[(int)count];
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return Encoding.UTF8.GetString(buffer, 0, totalRead);
    }

    private static string NormalizeNewLines(string text)
    {
        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static bool RequiresLeadingTrim(FileStream stream, long offset)
    {
        if (offset <= 0)
        {
            return false;
        }

        stream.Seek(offset - 1, SeekOrigin.Begin);
        var previousByte = stream.ReadByte();
        return previousByte != '\n' && previousByte != '\r';
    }

    private static string SkipLeadingPartialLine(string text)
    {
        var separatorIndex = text.IndexOf('\n');
        return separatorIndex < 0 ? string.Empty : text[(separatorIndex + 1)..];
    }
}

internal sealed record LogTailSnapshot(IReadOnlyList<string> Lines, string? Message);
