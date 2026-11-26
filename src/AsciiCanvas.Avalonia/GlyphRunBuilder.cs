using Avalonia.Media;

namespace AsciiCanvas.Avalonia;

/// <summary>Collects neighbouring cells that share a color and font style, so they can be drawn as one glyph run.</summary>
internal sealed class GlyphRunBuilder
{
    private ushort[] _glyphs = new ushort[128];
    private char[] _characters = new char[128];
    private bool _bold;
    private bool _italic;
    private ushort _space;

    /// <summary>Gets the number of glyphs collected, spaces included.</summary>
    public int Count { get; private set; }

    /// <summary>Gets the column of the first glyph.</summary>
    public int StartColumn { get; private set; }

    /// <summary>Gets the color shared by the glyphs, as 0xRRGGBB.</summary>
    public uint Foreground { get; private set; }

    /// <summary>Gets the font face shared by the glyphs.</summary>
    public GlyphTypeface? Face { get; private set; }

    /// <summary>Gets the glyph indices; only the first <see cref="Count"/> are valid.</summary>
    public ushort[] Glyphs => _glyphs;

    /// <summary>Gets the characters of the glyphs; only the first <see cref="Count"/> are valid.</summary>
    public char[] Characters => _characters;

    /// <summary>Whether a glyph with this color and style can join the run.</summary>
    public bool Accepts(uint foreground, bool bold, bool italic) =>
        Foreground == foreground && _bold == bold && _italic == italic;

    /// <summary>Starts a new run at <paramref name="column"/>. The run must be empty.</summary>
    public void Start(int column, uint foreground, bool bold, bool italic, GlyphTypeface face)
    {
        StartColumn = column;
        Foreground = foreground;
        _bold = bold;
        _italic = italic;
        Face = face;
        _space = face.CharacterToGlyphMap.TryGetGlyph(' ', out var space) ? space : (ushort)0;
    }

    /// <summary>Adds a glyph in the next column.</summary>
    public void Append(ushort glyph, char character)
    {
        if (Count == _glyphs.Length)
        {
            Array.Resize(ref _glyphs, Count * 2);
            Array.Resize(ref _characters, Count * 2);
        }

        _glyphs[Count] = glyph;
        _characters[Count] = character;
        Count++;
    }

    /// <summary>Adds a space in the next column; false when the font has no space glyph.</summary>
    public bool TryAppendSpace()
    {
        if (_space == 0)
        {
            return false;
        }

        Append(_space, ' ');
        return true;
    }

    /// <summary>Drops the spaces at the end of the run; they have no ink.</summary>
    public void TrimTrailingSpaces()
    {
        while (Count > 0 && _characters[Count - 1] == ' ')
        {
            Count--;
        }
    }

    /// <summary>Empties the run so the builder can be reused.</summary>
    public void Clear()
    {
        Count = 0;
        Face = null;
    }
}
