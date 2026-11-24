using EdgeHub.ConsoleMode;

namespace EdgeHub.Tests.ConsoleMode;

public sealed class ConsoleCommandsTests
{
    [Theory]
    [InlineData(ConsoleKey.Spacebar, ConsoleCommand.PlayPause)]
    [InlineData(ConsoleKey.N, ConsoleCommand.Next)]
    [InlineData(ConsoleKey.P, ConsoleCommand.Previous)]
    [InlineData(ConsoleKey.Q, ConsoleCommand.Quit)]
    [InlineData(ConsoleKey.Escape, ConsoleCommand.Quit)]
    [InlineData(ConsoleKey.A, ConsoleCommand.None)]
    public void Maps_each_key_to_its_command(ConsoleKey key, ConsoleCommand expected)
    {
        Assert.Equal(expected, ConsoleCommands.FromKey(key));
    }
}
