using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    [Theory]
    [InlineData("/")]
    [InlineData("/manifest.webmanifest")]
    [InlineData("/icon.svg")]
    public async Task The_screen_loads_nothing_from_another_site(string path)
    {
        // An XML namespace (xmlns="http://www.w3.org/2000/svg") names a vocabulary: nothing is downloaded from it.
        string file = Regex.Replace(await factory.CreateClient().GetStringAsync(path, Ct), @"xmlns(?::\w+)?\s*=\s*""[^""]*""", "");

        // Any absolute address (href, src, srcset, url(), fetch, EventSource, WebSocket, import), or one that keeps the
        // scheme of the page ("//host/…").
        Assert.DoesNotMatch(new Regex("""(?:https?|wss?)://""", RegexOptions.IgnoreCase), file);
        Assert.DoesNotMatch(new Regex("""["'(=]\s*//[^/\s]"""), file);
        Assert.DoesNotMatch(new Regex("""@import""", RegexOptions.IgnoreCase), file);
    }

    [Fact]
    public async Task Every_font_face_is_the_woff2_file_of_its_family_and_weight_served_by_the_pc()
    {
        HttpClient client = factory.CreateClient();
        string page = await client.GetStringAsync("/", Ct);
        string[] faces = Regex.Matches(page, """@font-face\s*\{([^}]*)\}""").Select(match => match.Groups[1].Value).ToArray();

        // Chakra Petch 500 and 600, IBM Plex Sans 400, 500, 600 and 700.
        Assert.Equal(6, faces.Length);
        foreach (string face in faces)
        {
            string family = Regex.Match(face, """font-family\s*:\s*"([^"]+)"\s*;""").Groups[1].Value;
            string weight = Regex.Match(face, """font-weight\s*:\s*(\d+)\s*;""").Groups[1].Value;
            string font = Regex.Match(face, """src\s*:\s*url\(\s*["']?([^"')]+)""").Groups[1].Value;

            // The files are named after their family and weight (chakra-petch-600.woff2), so a face that names the
            // wrong family, weight or file shows here.
            Assert.Equal($"/fonts/{family.Replace(' ', '-')}-{weight}.woff2", font, ignoreCase: true);
            using var response = await client.GetAsync(font, Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("font/woff2", response.Content.Headers.ContentType?.MediaType);
            byte[] content = await response.Content.ReadAsByteArrayAsync(Ct);
            Assert.Equal("wOF2"u8.ToArray(), content.Take(4));
        }
    }

    [Theory]
    [InlineData("--font-display")]
    [InlineData("--font-body")]
    public async Task The_font_the_page_asks_for_first_is_one_it_serves(string variable)
    {
        string page = await factory.CreateClient().GetStringAsync("/", Ct);

        string wanted = Regex.Match(page, variable + """\s*:\s*"([^"]+)"[^;]*;""").Groups[1].Value;
        string[] served = Regex.Matches(page, """@font-face\s*\{[^}]*font-family\s*:\s*"([^"]+)"[^}]*\}""")
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.NotEmpty(wanted);
        Assert.Contains(wanted, served);
    }

    [Fact]
    public async Task The_manifest_icon_is_served()
    {
        using var response = await factory.CreateClient().GetAsync("/icon.svg", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
    }
}
