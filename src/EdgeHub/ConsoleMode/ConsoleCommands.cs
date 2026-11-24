namespace EdgeHub.ConsoleMode;

public enum ConsoleCommand
{
    None,
    PlayPause,
    Next,
    Previous,
    Quit,
}

public static class ConsoleCommands
{
    public static ConsoleCommand FromKey(ConsoleKey key) => key switch
    {
        ConsoleKey.Spacebar => ConsoleCommand.PlayPause,
        ConsoleKey.N => ConsoleCommand.Next,
        ConsoleKey.P => ConsoleCommand.Previous,
        ConsoleKey.Q or ConsoleKey.Escape => ConsoleCommand.Quit,
        _ => ConsoleCommand.None,
    };

    /// <summary>The same commands, for keys piped in as characters by a script.</summary>
    public static ConsoleCommand FromChar(char character) => char.ToLowerInvariant(character) switch
    {
        ' ' => ConsoleCommand.PlayPause,
        'n' => ConsoleCommand.Next,
        'p' => ConsoleCommand.Previous,
        'q' => ConsoleCommand.Quit,
        _ => ConsoleCommand.None,
    };
}
