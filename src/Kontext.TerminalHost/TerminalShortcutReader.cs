namespace Kontext.TerminalHost;

public static class TerminalShortcutReader
{
    private const int VkReturn = 0x0D;
    private const int VkInsert = 0x2D;
    private const int VkC = 0x43;
    private const int VkV = 0x56;

    /// <summary>
    /// What Shift+Enter sends: Escape then Enter, the sequence Claude (and most shells) read as "new line, do not submit".
    /// The terminal itself would send the same code as plain Enter.
    /// </summary>
    public const string NewLineInput = "\u001b\r";

    public static TerminalShortcut? FromKey(int virtualKey, bool control, bool shift, bool alt)
    {
        if (alt)
        {
            return null;
        }

        return virtualKey switch
        {
            VkReturn when shift && !control => TerminalShortcut.NewLine,
            VkV when control => TerminalShortcut.Paste,
            VkInsert when shift && !control => TerminalShortcut.Paste,
            VkInsert when control && !shift => TerminalShortcut.Copy,
            VkC when control && shift => TerminalShortcut.Copy,
            _ => null,
        };
    }
}
