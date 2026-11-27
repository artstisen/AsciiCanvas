using Avalonia.Media;

namespace AsciiCanvas.Avalonia;

/// <summary>
/// Monospaced fonts embedded in this library, so they look the same on every platform.
/// Both are licensed under the SIL Open Font License 1.1 (see THIRD-PARTY-NOTICES.md).
/// </summary>
public static class CanvasFonts
{
    private const string FontFolder = "avares://AsciiCanvas.Avalonia/Assets/Fonts";

    private static readonly FontFamily _cascadiaMono = new($"{FontFolder}#Cascadia Mono");
    private static readonly FontFamily _jetBrainsMono = new($"{FontFolder}#JetBrains Mono");

    /// <summary>Cascadia Mono, Microsoft's console font. Regular, bold, italic and bold italic.</summary>
    public static FontFamily CascadiaMono => _cascadiaMono;

    /// <summary>JetBrains Mono, designed for reading code. Regular, bold, italic and bold italic. Ligatures cannot form because every cell is drawn on its own.</summary>
    public static FontFamily JetBrainsMono => _jetBrainsMono;

    /// <summary>The font used unless one is chosen: <see cref="CascadiaMono"/>.</summary>
    public static FontFamily Default => _cascadiaMono;

    /// <summary>
    /// An embedded font by its name ("Cascadia Mono", "JetBrains Mono", in any letter case), as <see cref="CellCanvas.FontName"/>
    /// gives it; <see langword="null"/> for another name.
    /// </summary>
    /// <param name="name">The name of the font.</param>
    public static FontFamily? FromName(string name) => name.Trim().ToUpperInvariant() switch
    {
        "CASCADIA MONO" => _cascadiaMono,
        "JETBRAINS MONO" => _jetBrainsMono,
        _ => null,
    };
}
