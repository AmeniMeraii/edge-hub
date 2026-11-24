using System.ComponentModel.DataAnnotations;

namespace EdgeHub.Configuration;

/// <summary>Section <c>Server</c>.</summary>
public sealed class ServerOptions
{
    public const string Section = "Server";

    [Range(1, 65535)]
    public int Port { get; set; } = 5080;

    /// <summary>
    /// False by default: the service only answers on this PC. The tablet needs true, plus a firewall rule for the port
    /// on the Private profile.
    /// </summary>
    public bool ListenOnNetwork { get; set; }
}
