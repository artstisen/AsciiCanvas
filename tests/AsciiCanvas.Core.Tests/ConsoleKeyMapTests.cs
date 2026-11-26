namespace AsciiCanvas.Tests;

public class ConsoleKeyMapTests
{
    [Fact]
    public void FromInput_Paste_ReturnsCharactersWithEnterForLineBreaks()
    {
        var keys = ConsoleKeyMap.FromInput(new PasteEvent("x\r\ny\tz"));

        Assert.Equal(
            [ConsoleKey.X, ConsoleKey.Enter, ConsoleKey.Y, ConsoleKey.Tab, ConsoleKey.Z],
            keys.Select(k => k.Key));
        Assert.Equal("x\ry\tz", string.Concat(keys.Select(k => k.KeyChar)));
    }

    [Fact]
    public void FromInput_TypedText_ReturnsOneKeyPerCharacter()
    {
        var keys = ConsoleKeyMap.FromInput(new TextEvent("Hi"));

        Assert.Equal([('H', ConsoleKey.H), ('i', ConsoleKey.I)], keys.Select(k => (k.KeyChar, k.Key)));
        Assert.Equal(ConsoleModifiers.Shift, keys[0].Modifiers);
    }

    [Fact]
    public void FromInput_ActionKey_ReturnsTheConsoleKeyWithModifiers()
    {
        var keys = ConsoleKeyMap.FromInput(new KeyEvent(Key.Left, KeyModifiers.Control | KeyModifiers.Shift));

        var key = Assert.Single(keys);
        Assert.Equal(ConsoleKey.LeftArrow, key.Key);
        Assert.Equal(ConsoleModifiers.Control | ConsoleModifiers.Shift, key.Modifiers);
    }

    [Theory]
    [MemberData(nameof(EventsWithoutKeys))]
    public void FromInput_EventWithoutKeys_ReturnsEmpty(InputEvent input) => Assert.Empty(ConsoleKeyMap.FromInput(input));

    public static TheoryData<InputEvent> EventsWithoutKeys() =>
    [
        new KeyEvent(Key.A),
        new MouseEvent(1, 1, MouseButton.Left, MouseAction.Press),
        new ResizeEvent(10, 5),
        new PasteEvent(""),
    ];
}
