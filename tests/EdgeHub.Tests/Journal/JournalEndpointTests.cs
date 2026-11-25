using System.Globalization;
using System.Net;
using System.Text.Json;
using EdgeHub.Security;
using EdgeHub.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EdgeHub.Tests.Journal;

public sealed class JournalEndpointTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonDocument> GetAsync(EdgeHubFactory factory, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"{AccessTokenMiddleware.CookieName}={EdgeHubFactory.Token}");
        using var response = await factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }).SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData("/api/journal/today")]
    [InlineData("/api/claude/current")]
    public async Task Needs_the_token(string path)
    {
        await using var factory = new EdgeHubFactory();

        using var response = await factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }).GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/journal/today")]
    [InlineData("/api/claude/current")]
    public async Task Tests_never_read_the_developers_journal_or_sessions(string path)
    {
        await using var factory = new EdgeHubFactory();

        using JsonDocument answer = await GetAsync(factory, path);

        Assert.False(answer.RootElement.GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task Serves_the_preparation_of_today()
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory("edgehub-journal-");
        try
        {
            string today = TimeProvider.System.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "journal", "2026-11-25.md"), Path.Combine(folder.FullName, today + ".md"));
            await using var factory = new JournalFactory(folder.FullName);

            using JsonDocument answer = await GetAsync(factory, "/api/journal/today");

            JsonElement root = answer.RootElement;
            Assert.True(root.GetProperty("found").GetBoolean());
            Assert.Equal(today, root.GetProperty("day").GetString());
            JsonElement first = root.GetProperty("preparation").GetProperty("daily")[0];
            Assert.Equal("DEMO-101", first.GetProperty("ticket").GetString());
            Assert.Equal("oui", first.GetProperty("testable").GetString());
            Assert.Equal("applied", root.GetProperty("preparation").GetProperty("followUps")[0].GetProperty("status").GetString());
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    private sealed class JournalFactory(string folder) : EdgeHubFactory
    {
        protected override IReadOnlyDictionary<string, string?> ExtraSettings => new Dictionary<string, string?> { ["Journal:Folder"] = folder };
    }
}
