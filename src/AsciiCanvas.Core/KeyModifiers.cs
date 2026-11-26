namespace AsciiCanvas;

/// <summary>Modifier keys held during a key or mouse event. Combine with <c>|</c>.</summary>
[Flags]
public enum KeyModifiers
{
    /// <summary>No modifier.</summary>
    None = 0,

    /// <summary>A Shift key is held.</summary>
    Shift = 1,

    /// <summary>A Control key is held.</summary>
    Control = 2,

    /// <summary>An Alt key is held.</summary>
    Alt = 4,
}
