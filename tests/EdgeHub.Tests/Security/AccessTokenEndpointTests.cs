using System.Net;
using EdgeHub.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EdgeHub.Tests.Security;

public sealed class AccessTokenEndpointTests(EdgeHubFactory factory) : IClassFixture<EdgeHubFactory>
{
    private const string Cookie = "edgehub_k";

    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_api_route_without_a_token_answers_401()
    {
        using var response = await Client().GetAsync("/api/stream", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_api_route_with_a_wrong_token_answers_401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/stream");
        request.Headers.Add("Cookie", $"{Cookie}=not-the-token");

        using var response = await Client().SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Opening_the_page_with_the_token_stores_it_in_a_cookie_and_cleans_the_address()
    {
        using var response = await Client().GetAsync($"/?k={EdgeHubFactory.Token}", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
        string setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith($"{Cookie}={EdgeHubFactory.Token}", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Opening_the_page_with_a_wrong_token_answers_401_and_sets_no_cookie()
    {
        using var response = await Client().GetAsync("/?k=not-the-token", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task The_cookie_alone_opens_the_api_after_a_reload()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/stream");
        request.Headers.Add("Cookie", $"{Cookie}={EdgeHubFactory.Token}");

        using var response = await Client().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_health_route_stays_open_without_a_token()
    {
        using var response = await Client().GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
