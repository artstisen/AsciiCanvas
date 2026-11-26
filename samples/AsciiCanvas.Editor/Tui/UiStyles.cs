namespace AsciiCanvas.Editor.Tui;

/// <summary>Colors of the menus and dialogs: dark text on a light background, like a classic text-mode interface.</summary>
internal static class UiStyles
{
    public static readonly CellStyle MenuBar = new(CellColor.Black, CellColor.White);
    public static readonly CellStyle MenuBarActive = new(CellColor.BrightWhite, CellColor.Blue);
    public static readonly CellStyle Panel = new(CellColor.Black, CellColor.White);
    public static readonly CellStyle PanelSelected = new(CellColor.BrightWhite, CellColor.Blue);
    public static readonly CellStyle PanelDisabled = new(CellColor.BrightBlack, CellColor.White);
    public static readonly CellStyle PanelTitle = new(CellColor.Blue, CellColor.White, CellAttributes.Bold);
    public static readonly CellStyle Button = new(CellColor.Black, CellColor.Cyan);
    public static readonly CellStyle ButtonFocus = new(CellColor.BrightWhite, CellColor.Blue, CellAttributes.Bold);
    public static readonly CellStyle Field = new(CellColor.BrightWhite, CellColor.Black);
    public static readonly CellStyle Shadow = new(CellColor.BrightBlack, CellColor.Black);
}
