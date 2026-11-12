namespace Anitec.Gateway;

/// <summary>Settings of the gateway itself (the routes and clusters live in the ReverseProxy section).</summary>
public class GatewayOptions
{
    /// <summary>API versions the gateway accepts in the path, for example <c>v1</c>. Any other version is rejected. Empty means only v1.</summary>
    public string[] SupportedApiVersions { get; set; } = [];

    /// <summary>
    ///     Publishes <c>/gateway/status</c> with the health of each destination. It lists replica names, so it
    ///     is off unless the environment turns it on (Gateway__ExposeStatus=true).
    /// </summary>
    public bool ExposeStatus { get; set; }
}
