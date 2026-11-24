using System.Net;
using EdgeHub.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EdgeHub.Tests.LogFiles;

public sealed class LogFileTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_log_file_records_requests_but_never_the_token_even_with_every_log_on()
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory("edgehub-logs-");
        try
        {
            await using (var factory = new LogFileFactory(folder.FullName))
            {
                HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
                using var opened = await client.GetAsync($"/?k={EdgeHubFactory.Token}", Ct);
                using var page = await client.GetAsync("/", Ct);
                Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            }

            // The service has stopped, so the file is closed and complete.
            string logs = string.Concat(folder.GetFiles("edgehub-*.log").Select(file => File.ReadAllText(file.FullName)));
            Assert.Contains("/index.html", logs, StringComparison.Ordinal);
            Assert.DoesNotContain(EdgeHubFactory.Token, logs, StringComparison.Ordinal);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    /// <summary>Writes the log file to a test folder, with every category of the file set to Trace.</summary>
    private sealed class LogFileFactory(string folder) : EdgeHubFactory
    {
        protected override IReadOnlyDictionary<string, string?> ExtraSettings => new Dictionary<string, string?>
        {
            ["Logs:Folder"] = folder,
            ["Logging:Serilog:LogLevel:Default"] = "Trace",
        };
    }
}
