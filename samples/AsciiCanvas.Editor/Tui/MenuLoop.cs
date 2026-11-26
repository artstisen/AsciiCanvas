namespace AsciiCanvas.Editor.Tui;

/// <summary>Runs an open menu: follows the mouse and the keys until an entry is chosen or the menu is dismissed.</summary>
internal static class MenuLoop
{
    /// <summary>Shows a drop-down menu and waits.</summary>
    /// <param name="host">The canvas and the editor background.</param>
    /// <param name="bar">The menu bar.</param>
    /// <param name="startMenu">The menu to open.</param>
    /// <param name="fromKeyboard">True to highlight the first entry right away, as when opened with a key.</param>
    /// <param name="isEnabled">Tells whether a command can be used now.</param>
    /// <returns>The chosen command, or <see langword="null"/> if the menu was dismissed.</returns>
    public static async Task<EditorCommand?> RunAsync(ModalHost host, MenuBar bar, int startMenu, bool fromKeyboard, Func<EditorCommand, bool> isEnabled)
    {
        var canvas = host.Canvas;
        var active = startMenu;
        var selected = fromKeyboard ? Step(bar, active, -1, 1, isEnabled) : -1;

        try
        {
            while (true)
            {
                bar.Active = active;
                host.DrawBackground();
                bar.DrawDropdown(canvas, active, selected, isEnabled);
                canvas.CursorVisible = false;
                canvas.Present();

                switch (await canvas.Input.ReadAsync(canvas.Closed))
                {
                    case MouseEvent { Action: MouseAction.Move } move:
                        var overTitle = bar.TitleAt(move.Column, move.Row);
                        if (overTitle >= 0 && overTitle != active)
                        {
                            active = overTitle;
                            selected = -1;
                        }
                        else
                        {
                            var hover = bar.ItemAt(active, move.Column, move.Row);
                            selected = hover >= 0 && IsUsable(bar, active, hover, isEnabled) ? hover : -1;
                        }

                        break;

                    case MouseEvent { Action: MouseAction.Press, Button: MouseButton.Left } press:
                        var title = bar.TitleAt(press.Column, press.Row);
                        var item = bar.ItemAt(active, press.Column, press.Row);
                        if (title >= 0)
                        {
                            if (title == active)
                            {
                                return null;
                            }

                            active = title;
                            selected = -1;
                        }
                        else if (item >= 0)
                        {
                            if (IsUsable(bar, active, item, isEnabled))
                            {
                                return bar.Menus[active].Items[item].Command;
                            }
                        }
                        else
                        {
                            return null;
                        }

                        break;

                    case KeyEvent { Key: Key.Escape or Key.F10 }:
                        return null;
                    case KeyEvent { Key: Key.Down }:
                        selected = Step(bar, active, selected, 1, isEnabled);
                        break;
                    case KeyEvent { Key: Key.Up }:
                        selected = Step(bar, active, selected, -1, isEnabled);
                        break;
                    case KeyEvent { Key: Key.Left }:
                        active = (active - 1 + bar.Menus.Count) % bar.Menus.Count;
                        selected = Step(bar, active, -1, 1, isEnabled);
                        break;
                    case KeyEvent { Key: Key.Right }:
                        active = (active + 1) % bar.Menus.Count;
                        selected = Step(bar, active, -1, 1, isEnabled);
                        break;
                    case KeyEvent { Key: Key.Enter } when selected >= 0:
                        return bar.Menus[active].Items[selected].Command;

                    case CloseRequestedEvent closeRequest:
                        // The window is being closed while a menu is open: dismiss the menu and let the editor handle the request.
                        canvas.Input.Post(closeRequest);
                        return null;
                }
            }
        }
        finally
        {
            bar.Active = null;
        }
    }

    private static bool IsUsable(MenuBar bar, int menu, int item, Func<EditorCommand, bool> isEnabled) =>
        bar.Menus[menu].Items[item].Command is { } command && isEnabled(command);

    // Finds the next usable entry in a direction, wrapping around; -1 if there is none.
    private static int Step(MenuBar bar, int menu, int from, int direction, Func<EditorCommand, bool> isEnabled)
    {
        var count = bar.Menus[menu].Items.Count;
        var index = from;
        for (var i = 0; i < count; i++)
        {
            index = ((index + direction) % count + count) % count;
            if (IsUsable(bar, menu, index, isEnabled))
            {
                return index;
            }
        }

        return -1;
    }
}
