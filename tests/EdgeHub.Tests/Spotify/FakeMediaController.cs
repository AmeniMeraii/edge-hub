using EdgeHub.Spotify;

namespace EdgeHub.Tests.Spotify;

/// <summary>Records the commands it receives and accepts or refuses them all.</summary>
internal sealed class FakeMediaController(bool accept) : IMediaController
{
    public List<string> Commands { get; } = [];

    public MediaState? State { get; set; }

    /// <summary>The state the media app moves to once it has accepted a command, when the test sets one.</summary>
    public MediaState? StateAfterCommand { get; set; }

    public Task<MediaState?> GetStateAsync(CancellationToken cancellationToken) => Task.FromResult(State);

    public Task<bool> PlayPauseAsync(CancellationToken cancellationToken) => Record("play-pause");

    public Task<bool> NextAsync(CancellationToken cancellationToken) => Record("next");

    public Task<bool> PreviousAsync(CancellationToken cancellationToken) => Record("previous");

    private Task<bool> Record(string command)
    {
        Commands.Add(command);
        if (accept && StateAfterCommand is not null)
        {
            State = StateAfterCommand;
        }

        return Task.FromResult(accept);
    }
}
