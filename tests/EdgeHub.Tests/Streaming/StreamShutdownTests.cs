using System.Diagnostics;
using EdgeHub.Security;
using EdgeHub.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EdgeHub.Tests.Streaming;

public sealed class StreamShutdownTests
{
    [Fact]
    public async Task An_open_stream_ends_as_soon_as_the_service_stops()
    {
        using var factory = new EdgeHubFactory();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/stream");
        request.Headers.Add("Cookie", $"{AccessTokenMiddleware.CookieName}={EdgeHubFactory.Token}");
        using var response = await factory.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
        while (await reader.ReadLineAsync(timeout.Token) is { } line && !line.StartsWith("data: ", StringComparison.Ordinal))
        {
            // Wait for the first event: the stream is open.
        }

        var watch = Stopwatch.StartNew();
        factory.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        while (await reader.ReadLineAsync(timeout.Token) is not null)
        {
            // Drain until the server ends the stream.
        }

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"The stream took {watch.Elapsed} to end.");
    }
}
