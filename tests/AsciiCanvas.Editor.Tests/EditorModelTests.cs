namespace AsciiCanvas.Editor.Tests;

public class EditorModelTests
{
    [Fact]
    public void InsertText_MultiLineText_SplitsLinesAndMovesCaretToEnd()
    {
        var model = new EditorModel("ab");
        model.MoveToEnd(extend: false);

        model.InsertText("c\r\nde\nf");

        Assert.Equal("abc\nde\nf", model.Text);
        Assert.Equal(new TextPosition(2, 1), model.Caret);
        Assert.False(model.HasSelection);
    }

    [Fact]
    public void InsertText_WithSelection_ReplacesSelection()
    {
        var model = new EditorModel("hello world");
        model.MoveTo(new TextPosition(0, 6), extend: false);
        model.MoveToEnd(extend: true);

        model.InsertText("there");

        Assert.Equal("hello there", model.Text);
    }

    [Fact]
    public void InsertNewLine_IndentedLine_RepeatsIndentation()
    {
        var model = new EditorModel("    foo");
        model.MoveToEnd(extend: false);

        model.InsertNewLine();

        Assert.Equal("    foo\n    ", model.Text);
        Assert.Equal(new TextPosition(1, 4), model.Caret);
    }

    [Fact]
    public void InsertTab_AfterThreeCharacters_PadsToNextTabStop()
    {
        var model = new EditorModel("abc");
        model.MoveToEnd(extend: false);

        model.InsertTab();

        Assert.Equal("abc ", model.Text);
    }

    [Fact]
    public void Backspace_AtStartOfLine_JoinsWithPreviousLine()
    {
        var model = new EditorModel("ab\ncd");
        model.MoveTo(new TextPosition(1, 0), extend: false);

        model.Backspace();

        Assert.Equal("abcd", model.Text);
        Assert.Equal(new TextPosition(0, 2), model.Caret);
    }

    [Fact]
    public void Backspace_AfterSurrogatePair_DeletesTheWholeCharacter()
    {
        var model = new EditorModel("a😀");
        model.MoveToEnd(extend: false);

        model.Backspace();

        Assert.Equal("a", model.Text);
    }

    [Fact]
    public void DeleteForward_AtEndOfLine_JoinsNextLine()
    {
        var model = new EditorModel("ab\ncd");
        model.MoveTo(new TextPosition(0, 2), extend: false);

        model.DeleteForward();

        Assert.Equal("abcd", model.Text);
    }

    [Fact]
    public void MoveDown_ThroughShortLine_RestoresPreferredColumn()
    {
        var model = new EditorModel("abcdef\nxy\nabcdef");
        model.MoveTo(new TextPosition(0, 5), extend: false);

        model.MoveDown(extend: false);
        var onShortLine = model.Caret;
        model.MoveDown(extend: false);

        Assert.Equal(new TextPosition(1, 2), onShortLine);
        Assert.Equal(new TextPosition(2, 5), model.Caret);
    }

    [Fact]
    public void MoveRight_WithExtend_SelectsAcrossLines()
    {
        var model = new EditorModel("ab\ncd");
        model.MoveTo(new TextPosition(0, 1), extend: false);

        model.MoveRight(extend: true);
        model.MoveRight(extend: true);
        model.MoveRight(extend: true);

        Assert.Equal("b\nc", model.SelectedText);
        Assert.Equal(new TextPosition(0, 1), model.SelectionStart);
        Assert.Equal(new TextPosition(1, 1), model.SelectionEnd);
    }

    [Fact]
    public void MoveLeft_WithSelection_CollapsesToSelectionStart()
    {
        var model = new EditorModel("abcdef");
        model.MoveTo(new TextPosition(0, 4), extend: false);
        model.MoveLeft(extend: true);
        model.MoveLeft(extend: true);

        model.MoveLeft(extend: false);

        Assert.Equal(new TextPosition(0, 2), model.Caret);
        Assert.False(model.HasSelection);
    }

    [Fact]
    public void MoveWordRight_FromWordStart_StopsAtStartOfNextWord()
    {
        var model = new EditorModel("foo  bar.baz");

        model.MoveWordRight(extend: false);
        var afterFirst = model.Caret;
        model.MoveWordRight(extend: false);
        var afterSecond = model.Caret;

        Assert.Equal(new TextPosition(0, 5), afterFirst);
        Assert.Equal(new TextPosition(0, 8), afterSecond);
    }

    [Fact]
    public void MoveHome_OnIndentedLine_TogglesBetweenIndentAndLineStart()
    {
        var model = new EditorModel("    code");
        model.MoveToEnd(extend: false);

        model.MoveHome(extend: false);
        var first = model.Caret.Column;
        model.MoveHome(extend: false);

        Assert.Equal(4, first);
        Assert.Equal(0, model.Caret.Column);
    }

    [Fact]
    public void Undo_TypedWord_UndoesInOneStepAndRedoRestores()
    {
        var model = new EditorModel("");
        foreach (var c in "hello")
        {
            model.InsertText(c.ToString());
        }

        var undone = model.Undo();
        var afterUndo = model.Text;
        var redone = model.Redo();

        Assert.True(undone);
        Assert.Equal("", afterUndo);
        Assert.True(redone);
        Assert.Equal("hello", model.Text);
        Assert.Equal(new TextPosition(0, 5), model.Caret);
    }

    [Fact]
    public void Undo_AfterDeletingSelection_RestoresTextAndSelection()
    {
        var model = new EditorModel("abcdef");
        model.MoveTo(new TextPosition(0, 1), extend: false);
        model.MoveTo(new TextPosition(0, 4), extend: true);
        model.DeleteSelection();

        model.Undo();

        Assert.Equal("abcdef", model.Text);
        Assert.Equal("bcd", model.SelectedText);
    }

    [Fact]
    public void Undo_NothingToUndo_ReturnsFalse() => Assert.False(new EditorModel("x").Undo());

    [Fact]
    public void IsModified_AfterMarkSaved_IsFalseUntilNextEdit()
    {
        var model = new EditorModel("x");
        model.InsertText("y");
        Assert.True(model.IsModified);

        model.MarkSaved();
        Assert.False(model.IsModified);

        model.InsertText("z");
        Assert.True(model.IsModified);
    }

    [Fact]
    public void SelectAll_Always_SelectsWholeText()
    {
        var model = new EditorModel("one\ntwo");

        model.SelectAll();

        Assert.Equal("one\ntwo", model.SelectedText);
    }
}
