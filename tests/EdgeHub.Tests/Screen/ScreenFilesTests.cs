using System.Net;
using System.Text.Json;
using EdgeHub.Tests.TestSupport;

namespace EdgeHub.Tests.Screen;

public sealed class ScreenFilesTests(EdgeHubFactory factory) : IClassFixture<EdgeHubFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_screen_is_served_at_the_root_without_a_token()
    {
        using var response = await factory.CreateClient().GetAsync("/", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<link rel=\"manifest\" href=\"/manifest.webmanifest\">", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_manifest_opens_the_screen_full_screen_in_landscape()
    {
        using var response = await factory.CreateClient().GetAsync("/manifest.webmanifest", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/manifest+json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument manifest = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("fullscreen", manifest.RootElement.GetProperty("display").GetString());
        Assert.Equal("landscape", manifest.RootElement.GetProperty("orientation").GetString());
        Assert.Equal("/", manifest.RootElement.GetProperty("start_url").GetString());
    }

    [Fact]
    public async Task The_manifest_icon_is_served()
    {
        using var response = await factory.CreateClient().GetAsync("/icon.svg", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
    }
}
