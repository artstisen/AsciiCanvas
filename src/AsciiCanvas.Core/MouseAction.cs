namespace AsciiCanvas;

/// <summary>What happened in a <see cref="MouseEvent"/>.</summary>
public enum MouseAction
{
    /// <summary>A button went down.</summary>
    Press,

    /// <summary>A button went up.</summary>
    Release,

    /// <summary>The pointer moved to another cell.</summary>
    Move,

    /// <summary>The wheel turned.</summary>
    Wheel,
}
