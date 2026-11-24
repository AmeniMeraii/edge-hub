using EdgeHub.Spotify;

namespace EdgeHub.Tests.Spotify;

public sealed class MediaSessionPickerTests
{
    [Fact]
    public void Picks_spotify_even_when_another_app_holds_the_current_session()
    {
        Assert.Equal("Spotify.exe", MediaSessionPicker.Pick(["chrome", "Spotify.exe"], currentAppId: "chrome"));
    }

    [Fact]
    public void Recognises_the_store_version_of_spotify_whatever_the_case()
    {
        const string storeSpotify = "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify";

        Assert.Equal(storeSpotify, MediaSessionPicker.Pick(["msedge", storeSpotify], currentAppId: "msedge"));
    }

    [Fact]
    public void Ignores_the_case_of_the_app_id()
    {
        Assert.Equal("spotify.exe", MediaSessionPicker.Pick(["chrome", "spotify.exe"], currentAppId: "chrome"));
    }

    [Fact]
    public void Falls_back_to_the_current_session_without_spotify()
    {
        Assert.Equal("chrome", MediaSessionPicker.Pick(["vlc", "chrome"], currentAppId: "chrome"));
    }

    [Fact]
    public void None_when_there_is_no_session()
    {
        Assert.Null(MediaSessionPicker.Pick([], currentAppId: null));
    }
}
