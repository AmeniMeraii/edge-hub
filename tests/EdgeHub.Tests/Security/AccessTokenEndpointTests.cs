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

    [Theory]
    [InlineData("/API/STREAM")]
    [InlineData("/Api/Stream/")]
    public async Task Api_routes_are_protected_whatever_their_case(string path)
    {
        using var response = await Client().GetAsync(path, Ct);

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
        Assert.Contains("max-age=34560000", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_uppercase_K_is_cleaned_from_the_address_too()
    {
        using var response = await Client().GetAsync($"/?K={EdgeHubFactory.Token}", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Cleaning_the_address_keeps_the_other_parameters()
    {
        using var response = await Client().GetAsync($"/?k={EdgeHubFactory.Token}&page=2", Ct);

        Assert.Equal("/?page=2", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Cleaning_the_address_never_sends_the_browser_to_another_site()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"http://localhost//evil.example/?k={EdgeHubFactory.Token}"));

        using var response = await Client().SendAsync(request, Ct);

        string location = response.Headers.Location?.OriginalString ?? "";
        Assert.StartsWith("/", location, StringComparison.Ordinal);
        Assert.False(location.StartsWith("//", StringComparison.Ordinal), location);
    }

    [Fact]
    public async Task A_valid_cookie_on_the_api_is_renewed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/stream");
        request.Headers.Add("Cookie", $"{Cookie}={EdgeHubFactory.Token}");

        using var response = await Client().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, Ct);

        string setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("max-age=34560000", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_request_from_another_site_cannot_change_anything_even_with_the_cookie()
    {
        // A page served on another port of the same PC is "same site" for cookies: only the Fetch metadata tells it apart.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/stream");
        request.Headers.Add("Cookie", $"{Cookie}={EdgeHubFactory.Token}");
        request.Headers.Add("Sec-Fetch-Site", "same-site");

        using var response = await Client().SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_request_from_the_screen_itself_passes_the_site_check()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/stream");
        request.Headers.Add("Cookie", $"{Cookie}={EdgeHubFactory.Token}");
        request.Headers.Add("Sec-Fetch-Site", "same-origin");

        using var response = await Client().SendAsync(request, Ct);

        // No POST route exists yet: getting past the guard means reaching routing, which answers 405.
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Opening_the_page_with_a_wrong_token_answers_401_and_sets_no_cookie()
    {
        using var response = await Client().GetAsync("/?k=not-the-token", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Contains("Jeton refusé", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
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
