using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EdgeHub.Security;
using EdgeHub.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EdgeHub.Tests.Metrics;

public sealed class MetricsEndpointTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("edgehub-metrics-");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Events => Path.Combine(_folder.FullName, "events.jsonl");

    public void Dispose() => _folder.Delete(recursive: true);

    private static async Task<HttpResponseMessage> PostAsync(EdgeHubFactory factory, object body, bool withToken = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/events") { Content = JsonContent.Create(body) };
        request.Headers.Add("Sec-Fetch-Site", "same-origin");
        if (withToken)
        {
            request.Headers.Add("Cookie", $"{AccessTokenMiddleware.CookieName}={EdgeHubFactory.Token}");
        }

        return await factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }).SendAsync(request, Ct);
    }

    [Fact]
    public async Task Needs_the_token()
    {
        await using var factory = new MetricsFactory(_folder.FullName);

        using HttpResponseMessage response = await PostAsync(factory, new { type = "focus-completed" }, withToken: false);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Keeps_an_event_from_the_screen()
    {
        await using var factory = new MetricsFactory(_folder.FullName);

        using HttpResponseMessage response = await PostAsync(factory, new { type = "task-done", at = DateTimeOffset.UtcNow.AddMinutes(-3), task = "DEMO-101 Export CSV" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        string line = Assert.Single(await File.ReadAllLinesAsync(Events, Ct));
        Assert.Contains("\"task\":\"DEMO-101 Export CSV\"", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-a-type", 0)]
    [InlineData("preparation-seen", 0)]
    [InlineData("focus-completed", -9 * 24 * 60)]
    [InlineData("focus-completed", 60)]
    public async Task Refuses_an_unknown_event_or_one_far_from_now(string type, int minutesFromNow)
    {
        await using var factory = new MetricsFactory(_folder.FullName);

        using HttpResponseMessage response = await PostAsync(factory, new { type, at = DateTimeOffset.UtcNow.AddMinutes(minutesFromNow) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(File.Exists(Events));
    }

    [Fact]
    public async Task Tests_never_write_into_the_developers_metrics()
    {
        await using var factory = new EdgeHubFactory();

        using HttpResponseMessage response = await PostAsync(factory, new { type = "focus-completed" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/metrics/week");
        request.Headers.Add("Cookie", $"{AccessTokenMiddleware.CookieName}={EdgeHubFactory.Token}");
        using HttpResponseMessage week = await factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }).SendAsync(request, Ct);
        using JsonDocument answer = JsonDocument.Parse(await week.Content.ReadAsStringAsync(Ct));
        Assert.False(answer.RootElement.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Serves_the_week()
    {
        await using var factory = new MetricsFactory(_folder.FullName);
        await PostAsync(factory, new { type = "focus-completed" });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/metrics/week");
        request.Headers.Add("Cookie", $"{AccessTokenMiddleware.CookieName}={EdgeHubFactory.Token}");
        using HttpResponseMessage response = await factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }).SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument answer = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.True(answer.RootElement.GetProperty("enabled").GetBoolean());
        Assert.Equal(1, answer.RootElement.GetProperty("totals").GetProperty("focusCompleted").GetInt32());
    }

    private sealed class MetricsFactory(string folder) : EdgeHubFactory
    {
        protected override IReadOnlyDictionary<string, string?> ExtraSettings => new Dictionary<string, string?> { ["Metrics:Folder"] = folder };
    }
}
