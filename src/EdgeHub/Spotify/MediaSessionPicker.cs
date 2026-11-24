namespace EdgeHub.Spotify;

public static class MediaSessionPicker
{
    /// <summary>
    /// The media session to show: Spotify's when it has one (desktop or Store version), otherwise the session Windows
    /// considers current.
    /// </summary>
    public static string? Pick(IReadOnlyList<string> sourceAppIds, string? currentAppId) =>
        sourceAppIds.FirstOrDefault(id => id.Contains("Spotify", StringComparison.OrdinalIgnoreCase)) ?? currentAppId;
}
