using System.IO;
using System.Text;

namespace ChatSystem.Protocol;

// Bounded newline framing: a peer cannot force ReadLineAsync to allocate an entire large file.
public sealed class JsonLineReader(StreamReader reader, int maxCharacters)
{
    private readonly char[] _buffer = new char[4096];
    private int _position, _count;
    public async Task<string?> ReadLineAsync(CancellationToken ct = default)
    {
        var line = new StringBuilder();
        while (true)
        {
            if (_position == _count)
            {
                _count = await reader.ReadAsync(_buffer.AsMemory(), ct).ConfigureAwait(false);
                _position = 0;
                if (_count == 0)
                {
                    if (line.Length != 0) throw new IOException("Truncated JSON frame.");
                    return null;
                }
            }
            int end = Array.IndexOf(_buffer, '\n', _position, _count - _position);
            int length = (end < 0 ? _count : end) - _position;
            if (line.Length + length > maxCharacters) throw new IOException("JSON frame exceeds the protocol limit.");
            line.Append(_buffer, _position, length);
            _position += length;
            if (end >= 0) { _position++; return line.ToString(); }
        }
    }
}
