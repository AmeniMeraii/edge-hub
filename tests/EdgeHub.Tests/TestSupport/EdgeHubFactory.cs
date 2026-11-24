using EdgeHub.Spotify;
using EdgeHub.SystemStats;
using EdgeHub.Tests.Spotify;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EdgeHub.Tests.TestSupport;

/// <summary>
/// The service in memory, with an invented token and fake stats and media, so tests never touch the PC's real
/// counters, Spotify, or the developer's own configuration.
/// </summary>
public class EdgeHubFactory : WebApplicationFactory<Program>
{
    public const string Token = "test-token-0123456789-abcdefghijklmnopqrstuv";

    internal FakeSystemStats Stats { get; } = new();

    internal FakeMediaController Media { get; } = new(accept: true);

    protected virtual string? ConfiguredToken => Token;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:Token"] = ConfiguredToken,
        }));
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ISystemStats>(Stats);
            services.AddSingleton<IMediaController>(Media);
        });
    }
}
