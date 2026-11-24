using EdgeHub.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;

namespace EdgeHub.Tests.Configuration;

public sealed class LocalSettingsTests
{
    [Fact]
    public void The_local_file_comes_after_the_other_files_and_before_the_environment_and_the_command_line()
    {
        var builder = new ConfigurationBuilder();
        builder.Sources.Add(new JsonConfigurationSource { Path = "appsettings.json", Optional = true });
        builder.Sources.Add(new JsonConfigurationSource { Path = "secrets.json", Optional = true });
        builder.Sources.Add(new EnvironmentVariablesConfigurationSource());
        builder.Sources.Add(new CommandLineConfigurationSource { Args = [] });

        builder.AddLocalSettings();

        var order = builder.Sources.Select(s => s is JsonConfigurationSource json ? json.Path : s.GetType().Name).ToList();
        Assert.Equal(
            ["appsettings.json", "secrets.json", "appsettings.Local.json", nameof(EnvironmentVariablesConfigurationSource), nameof(CommandLineConfigurationSource)],
            order);
    }
}
