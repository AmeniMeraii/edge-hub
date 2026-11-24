using EdgeHub.Spotify;
using EdgeHub.SystemStats;
using EdgeHub.Tests.Spotify;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
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

    /// <summary>Settings a test adds or replaces.</summary>
    protected virtual IReadOnlyDictionary<string, string?> ExtraSettings => new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": the developer's user secrets are not loaded. Their appsettings.Local.json is removed too.
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config =>
        {
            foreach (IConfigurationSource local in config.Sources
                .Where(s => s is JsonConfigurationSource { Path: "appsettings.Local.json" })
                .ToList())
            {
                config.Sources.Remove(local);
            }

            var settings = new Dictionary<string, string?>
            {
                ["Security:Token"] = ConfiguredToken,
                // No log file: tests never write into the developer's own logs.
                ["Logs:Folder"] = "",
            };
            foreach ((string key, string? value) in ExtraSettings)
            {
                settings[key] = value;
            }

            config.AddInMemoryCollection(settings);
        });
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ISystemStats>(Stats);
            services.AddSingleton<IMediaController>(Media);
        });
    }
}
