using EdgeHub.Spotify;

namespace EdgeHub.ConsoleMode;

public static class MediaCommands
{
    /// <summary>Sends a media command and returns the line the console shows about it, or null for other keys.</summary>
    public static async Task<string?> SendAsync(ConsoleCommand command, IMediaController media, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(media);

        (string Label, Func<CancellationToken, Task<bool>> Send)? action = command switch
        {
            ConsoleCommand.PlayPause => ("Lecture/pause", media.PlayPauseAsync),
            ConsoleCommand.Next => ("Suivant", media.NextAsync),
            ConsoleCommand.Previous => ("Précédent", media.PreviousAsync),
            _ => null,
        };
        if (action is not { } chosen)
        {
            return null;
        }

        bool accepted = await chosen.Send(cancellationToken).ConfigureAwait(false);
        return accepted ? $"{chosen.Label} envoyé" : $"{chosen.Label} refusé : aucune session média, ou l'appli a refusé";
    }
}
