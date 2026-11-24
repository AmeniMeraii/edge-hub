using EdgeHub.Configuration;
using EdgeHub.LogFiles;
using EdgeHub.Tests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EdgeHub.Tests.LogFiles;

public sealed class LogFileOptionsTests
{
    private static LogFileOptions Bind(Dictionary<string, string?> values)
    {
        values["Security:Token"] = EdgeHubFactory.Token;
        IConfiguration settings = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(settings)
            .AddEdgeHubOptions()
            .BuildServiceProvider();

        services.GetRequiredService<IStartupValidator>().Validate();
        return services.GetRequiredService<IOptions<LogFileOptions>>().Value;
    }

    [Fact]
    public void Logs_go_to_the_local_app_data_folder_for_two_weeks_by_default()
    {
        LogFileOptions options = Bind([]);

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.Equal(Path.Combine(localAppData, "EdgeHub", "logs"), options.ResolvedFolder());
        Assert.Equal(14, options.RetainedDays);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("366")]
    public void The_service_refuses_to_start_with_a_retention_out_of_range(string days)
    {
        var failure = Assert.Throws<OptionsValidationException>(() => Bind(new() { ["Logs:RetainedDays"] = days }));

        Assert.Contains("RetainedDays", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_folder_turns_the_log_file_off()
    {
        LogFileOptions options = Bind(new() { ["Logs:Folder"] = "" });

        Assert.Null(options.ResolvedFolder());
        Assert.Same(NullLoggerProvider.Instance, LogFile.CreateProvider(options));
    }

    [Fact]
    public void A_folder_may_name_an_environment_variable()
    {
        var options = new LogFileOptions { Folder = Path.Combine("%LOCALAPPDATA%", "EdgeHub", "logs") };

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.Equal(Path.Combine(localAppData, "EdgeHub", "logs"), options.ResolvedFolder());
    }

    [Fact]
    public void A_relative_folder_is_next_to_the_service_whatever_folder_it_starts_from()
    {
        var options = new LogFileOptions { Folder = "logs" };

        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "logs"), options.ResolvedFolder());
    }
}
