namespace AsciiCanvas.Editor.Tests;

public class MenuBarTests
{
    private readonly MenuBar _bar = MenuBar.CreateDefault();

    [Fact]
    public void TitleAt_CellsOverTitles_FindsTheMenu()
    {
        // " File " covers columns 0-5 and " Edit " covers columns 6-11.
        Assert.Equal(0, _bar.TitleAt(0, 0));
        Assert.Equal(0, _bar.TitleAt(5, 0));
        Assert.Equal(1, _bar.TitleAt(6, 0));
        Assert.Equal(1, _bar.TitleAt(11, 0));
    }

    [Fact]
    public void TitleAt_CellsOutsideTitles_ReturnsMinusOne()
    {
        Assert.Equal(-1, _bar.TitleAt(12, 0));
        Assert.Equal(-1, _bar.TitleAt(0, 1));
    }

    [Fact]
    public void DropdownRect_Always_HangsBelowItsTitleAndFitsItsEntries()
    {
        var file = _bar.DropdownRect(0);
        var edit = _bar.DropdownRect(1);

        Assert.Equal(0, file.X);
        Assert.Equal(1, file.Y);
        Assert.Equal(_bar.Menus[0].Items.Count + 2, file.Height);
        Assert.Equal(6, edit.X);
        Assert.True(file.Width >= "Save As...".Length + "Ctrl+Shift+S".Length);
    }

    [Fact]
    public void ItemAt_RowsInsideTheDropdown_FindsTheEntryAndIgnoresTheBorder()
    {
        var rect = _bar.DropdownRect(0);
        var inside = rect.X + 3;

        Assert.Equal(0, _bar.ItemAt(0, inside, rect.Y + 1));
        Assert.Equal(4, _bar.ItemAt(0, inside, rect.Y + 5));
        Assert.Equal(-1, _bar.ItemAt(0, rect.X, rect.Y + 1));
        Assert.Equal(-1, _bar.ItemAt(0, inside, rect.Y));
        Assert.Equal(-1, _bar.ItemAt(0, inside, rect.Bottom - 1));
    }

    [Fact]
    public void Menus_Default_HaveNewOpenSaveAndSaveAsInTheFileMenu()
    {
        var commands = _bar.Menus[0].Items.Where(i => !i.IsSeparator).Select(i => i.Command).ToArray();

        Assert.Equal(
            [EditorCommand.New, EditorCommand.Open, EditorCommand.Save, EditorCommand.SaveAs, EditorCommand.Exit],
            commands);
    }
}
