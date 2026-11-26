using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace EdgeHub.Tests.TestSupport;

/// <summary>
/// The in-memory server has no network: a request carries no remote address, as if it came from the PC itself. A test
/// stands for another device with the <see cref="Header"/> header.
/// </summary>
internal sealed class TestRemoteAddress : IStartupFilter
{
    public const string Header = "X-Test-Remote-Ip";

    /// <summary>An invented address of the tablet on the home network.</summary>
    public const string Tablet = "192.168.1.50";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, following) =>
        {
            if (context.Request.Headers.TryGetValue(Header, out var address))
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(address.ToString());
            }

            await following(context);
        });
        next(app);
    };
}
