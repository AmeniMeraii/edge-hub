using System.Net;
using System.Text.Json;
using EdgeHub.Security;
using EdgeHub.Spotify;
using EdgeHub.Tests.TestSupport;

namespace EdgeHub.Tests.Streaming;

public sealed class StreamEndpointTests : IClassFixture<EdgeHubFactory>
{
    private readonly EdgeHubFactory _factory;

    public StreamEndpointTests(EdgeHubFactory factory)
    {
        _factory = factory;
        _factory.Media.State = new MediaState("Spotify.exe", "Demo Song", "Demo Artist", "Demo Album", IsPlaying: true, TimeSpan.FromSeconds(83), TimeSpan.FromSeconds(225));
    }

    [Fact]
    public async Task The_stream_sends_the_stats_and_the_track_as_server_sent_events()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/stream");
        request.Headers.Add("Cookie", $"{AccessTokenMiddleware.CookieName}={EdgeHubFactory.Token}");

        using var response = await _factory.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
        string? eventType = null;
        string? data = null;
        while (data is null && await reader.ReadLineAsync(timeout.Token) is { } line)
        {
            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                eventType = line["event: ".Length..];
            }
            else if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                data = line["data: ".Length..];
            }
        }

        Assert.Equal("state", eventType);
        using JsonDocument message = JsonDocument.Parse(data!);
        Assert.Equal(12, message.RootElement.GetProperty("stats").GetProperty("cpuPercent").GetDouble());
        JsonElement media = message.RootElement.GetProperty("media");
        Assert.Equal("Demo Song", media.GetProperty("title").GetString());
        Assert.Equal(83_000, media.GetProperty("positionMs").GetInt64());
        Assert.Equal(225_000, media.GetProperty("durationMs").GetInt64());
        Assert.True(media.GetProperty("isSpotify").GetBoolean());
    }
}
