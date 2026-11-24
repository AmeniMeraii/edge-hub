using EdgeHub.ConsoleMode;
using EdgeHub.Tests.Spotify;

namespace EdgeHub.Tests.ConsoleMode;

public sealed class MediaCommandsTests
{
    [Theory]
    [InlineData(ConsoleCommand.PlayPause, "play-pause", "Lecture/pause envoyé")]
    [InlineData(ConsoleCommand.Next, "next", "Suivant envoyé")]
    [InlineData(ConsoleCommand.Previous, "previous", "Précédent envoyé")]
    public async Task Sends_the_command_to_the_media_session_and_reports_it(ConsoleCommand command, string sent, string status)
    {
        var media = new FakeMediaController(accept: true);

        string? result = await MediaCommands.SendAsync(command, media, TestContext.Current.CancellationToken);

        Assert.Equal([sent], media.Commands);
        Assert.Equal(status, result);
    }

    [Fact]
    public async Task Reports_a_refused_command()
    {
        var media = new FakeMediaController(accept: false);

        string? result = await MediaCommands.SendAsync(ConsoleCommand.Next, media, TestContext.Current.CancellationToken);

        Assert.StartsWith("Suivant refusé", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ConsoleCommand.None)]
    [InlineData(ConsoleCommand.Quit)]
    public async Task Sends_nothing_for_a_key_that_is_not_a_media_command(ConsoleCommand command)
    {
        var media = new FakeMediaController(accept: true);

        string? result = await MediaCommands.SendAsync(command, media, TestContext.Current.CancellationToken);

        Assert.Empty(media.Commands);
        Assert.Null(result);
    }
}
