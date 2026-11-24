using System.Net;
using EdgeHub.Tests.TestSupport;

namespace EdgeHub.Tests;

public sealed class HealthEndpointTests(EdgeHubFactory factory) : IClassFixture<EdgeHubFactory>
{
    [Fact]
    public async Task Health_returns_200()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
