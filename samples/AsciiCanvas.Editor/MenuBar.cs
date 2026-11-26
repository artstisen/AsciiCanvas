using AsciiCanvas.Editor.Tui;

namespace AsciiCanvas.Editor;

/// <summary>
/// The menu bar on the top row and its drop-down menus, drawn with cells. This class knows where everything is, so a click can
/// be turned into a menu or an entry, and draws it; <c>MenuLoop</c> runs an open menu.
/// </summary>
public sealed class MenuBar(IReadOnlyList<Menu> menus)
{
    /// <summary>The menus of the editor: File and Edit, with their shortcuts for this platform.</summary>
    public static MenuBar CreateDefault()
    {
        var key = OperatingSystem.IsMacOS() ? "Cmd+" : "Ctrl+";
        return new MenuBar(
        [
            new Menu("File",
            [
                new MenuItem("New", key + "N", EditorCommand.New),
                new MenuItem("Open...", key + "O", EditorCommand.Open),
                MenuItem.Separator,
                new MenuItem("Save", key + "S", EditorCommand.Save),
                new MenuItem("Save As...", key + "Shift+S", EditorCommand.SaveAs),
                MenuItem.Separator,
                new MenuItem("Exit", key + "Q", EditorCommand.Exit),
            ]),
            new Menu("Edit",
            [
                new MenuItem("Undo", key + "Z", EditorCommand.Undo),
                new MenuItem("Redo", key + "Y", EditorCommand.Redo),
                MenuItem.Separator,
                new MenuItem("Cut", key + "X", EditorCommand.Cut),
                new MenuItem("Copy", key + "C", EditorCommand.Copy),
                new MenuItem("Paste", key + "V", EditorCommand.Paste),
                MenuItem.Separator,
                new MenuItem("Select All", key + "A", EditorCommand.SelectAll),
            ]),
        ]);
    }

    /// <summary>The menus, left to right.</summary>
    public IReadOnlyList<Menu> Menus => menus;

    /// <summary>The index of the menu that is open, or <see langword="null"/>. Drawn highlighted.</summary>
    public int? Active { get; set; }

    /// <summary>Gets the area of a title in the bar. A title has a space on each side.</summary>
    /// <param name="index">The index of the menu.</param>
    public CellRect TitleRect(int index)
    {
        var x = 0;
        for (var i = 0; i < index; i++)
        {
            x += CharWidth.Of(menus[i].Title) + 2;
        }

        return new CellRect(x, 0, CharWidth.Of(menus[index].Title) + 2, 1);
    }

    /// <summary>Finds the title under a cell.</summary>
    /// <param name="column">The grid column.</param>
    /// <param name="row">The grid row.</param>
    /// <returns>The index of the menu, or -1.</returns>
    public int TitleAt(int column, int row)
    {
        for (var i = 0; i < menus.Count; i++)
        {
            if (TitleRect(i).Contains(column, row))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Gets the area of a drop-down menu, including its border. It hangs below its title.</summary>
    /// <param name="index">The index of the menu.</param>
    public CellRect DropdownRect(int index)
    {
        var items = menus[index].Items;
        var inner = items.Max(i => CharWidth.Of(i.Label) + (i.Shortcut.Length > 0 ? 4 + CharWidth.Of(i.Shortcut) : 0)) + 2;
        return new CellRect(TitleRect(index).X, 1, inner + 2, items.Count + 2);
    }

    /// <summary>Finds the entry of a drop-down menu under a cell.</summary>
    /// <param name="menuIndex">The index of the menu.</param>
    /// <param name="column">The grid column.</param>
    /// <param name="row">The grid row.</param>
    /// <returns>The index of the entry (a separator counts), or -1 if the cell is not on an entry.</returns>
    public int ItemAt(int menuIndex, int column, int row)
    {
        var rect = DropdownRect(menuIndex);
        var itemCount = menus[menuIndex].Items.Count;
        var index = row - rect.Y - 1;
        return column > rect.X && column < rect.Right - 1 && index >= 0 && index < itemCount ? index : -1;
    }

    /// <summary>Draws the bar on the top row, with text at the right (such as the file name).</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="rightText">The text to show at the right end.</param>
    /// <param name="reserve">The number of columns at the right end to leave free, for something else drawn there.</param>
    public void DrawBar(CellCanvas canvas, string rightText, int reserve = 0)
    {
        canvas.Fill(new CellRect(0, 0, canvas.Columns, 1), ' ', UiStyles.MenuBar);
        for (var i = 0; i < menus.Count; i++)
        {
            var rect = TitleRect(i);
            var style = Active == i ? UiStyles.MenuBarActive : UiStyles.MenuBar;
            canvas.Write(rect.X, 0, " " + menus[i].Title + " ", style);
        }

        var room = canvas.Columns - TitleRect(menus.Count - 1).Right - 3 - reserve;
        var text = TextFit.Truncate(rightText, room);
        canvas.Write(canvas.Columns - CharWidth.Of(text) - 1 - reserve, 0, text, UiStyles.MenuBar);
    }

    /// <summary>Draws a drop-down menu with one entry highlighted.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="menuIndex">The index of the menu.</param>
    /// <param name="selected">The index of the highlighted entry, or -1.</param>
    /// <param name="isEnabled">Tells whether a command can be used now; disabled entries are dimmed.</param>
    public void DrawDropdown(CellCanvas canvas, int menuIndex, int selected, Func<EditorCommand, bool> isEnabled)
    {
        var rect = DropdownRect(menuIndex);
        DialogBox.DrawShadow(canvas, rect);
        canvas.Box(rect, BoxStyle.SingleLine, UiStyles.Panel, fill: true);

        var items = menus[menuIndex].Items;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var y = rect.Y + 1 + i;
            if (item.IsSeparator)
            {
                canvas.SetCell(rect.X, y, '├', UiStyles.Panel);
                canvas.HLine(rect.X + 1, y, rect.Width - 2, BoxStyle.SingleLine, UiStyles.Panel);
                canvas.SetCell(rect.Right - 1, y, '┤', UiStyles.Panel);
                continue;
            }

            var enabled = isEnabled(item.Command!.Value);
            var style = !enabled ? UiStyles.PanelDisabled : i == selected ? UiStyles.PanelSelected : UiStyles.Panel;
            canvas.Fill(new CellRect(rect.X + 1, y, rect.Width - 2, 1), ' ', style);
            canvas.Write(rect.X + 2, y, item.Label, style);
            canvas.Write(rect.Right - 2 - CharWidth.Of(item.Shortcut), y, item.Shortcut, style);
        }
    }
}
