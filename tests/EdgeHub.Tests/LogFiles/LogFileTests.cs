using System.Net;
using EdgeHub.LogFiles;
using EdgeHub.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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

    [Fact]
    public void The_file_keeps_request_and_outgoing_call_logs_out_even_without_the_logging_rules()
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory("edgehub-logs-");
        try
        {
            using (ILoggerProvider provider = LogFile.CreateProvider(new LogFileOptions { Folder = folder.FullName }))
            {
                provider.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics").Log(LogLevel.Information, default, "Request starting GET /?k=secret", null, (state, _) => state);
                provider.CreateLogger("System.Net.Http.HttpClient.calendar.LogicalHandler").Log(LogLevel.Information, default, "GET https://calendar.example.test/private.ics", null, (state, _) => state);
                provider.CreateLogger("Microsoft.AspNetCore.Server.Kestrel.BadRequests").Log(LogLevel.Debug, default, "Invalid request line: 'GET /?k=secret'", null, (state, _) => state);
                provider.CreateLogger("EdgeHub.Anything").Log(LogLevel.Information, default, "Other information", null, (state, _) => state);
            }

            string logs = string.Concat(folder.GetFiles("edgehub-*.log").Select(file => File.ReadAllText(file.FullName)));
            Assert.Contains("Other information", logs, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", logs, StringComparison.Ordinal);
            Assert.DoesNotContain("private.ics", logs, StringComparison.Ordinal);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_folder_that_cannot_be_written_to_means_no_log_file_rather_than_errors_on_every_line()
    {
        string notAFolder = Path.GetTempFileName();
        try
        {
            // A folder cannot be created under a file.
            Assert.Same(NullLoggerProvider.Instance, LogFile.CreateProvider(new LogFileOptions { Folder = Path.Combine(notAFolder, "logs") }));
        }
        finally
        {
            File.Delete(notAFolder);
        }
    }

    /// <summary>Writes the log file to a test folder, with every category of the file set to Trace.</summary>
    private sealed class LogFileFactory(string folder) : EdgeHubFactory
    {
        protected override IReadOnlyDictionary<string, string?> ExtraSettings => new Dictionary<string, string?>
        {
            ["Logs:Folder"] = folder,
            ["Logging:Serilog:LogLevel:Default"] = "Trace",
            ["Logging:Serilog:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics*"] = "Trace",
        };
    }
}
