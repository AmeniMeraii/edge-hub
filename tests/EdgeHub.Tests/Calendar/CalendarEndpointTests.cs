using System.Net;
using System.Text.Json;
using EdgeHub.Security;
using EdgeHub.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EdgeHub.Tests.Calendar;

public sealed class CalendarEndpointTests(EdgeHubFactory factory) : IClassFixture<EdgeHubFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    [Fact]
    public async Task Needs_the_token()
    {
        using var response = await Client().GetAsync("/api/calendar/today", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Answers_not_configured_without_a_calendar_link()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/calendar/today");
        request.Headers.Add("Cookie", $"{AccessTokenMiddleware.CookieName}={EdgeHubFactory.Token}");

        using var response = await Client().SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument today = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.False(today.RootElement.GetProperty("configured").GetBoolean());
    }
}
