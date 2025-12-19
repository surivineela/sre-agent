namespace Session.Proxy.Configuration;

public class IdentityProviderSettings
{
    public string BaseUrl { get; set; } = "http://localhost:12356";

    /// <summary>
    /// When true, the identity provider runs as a separate sidecar process.
    /// When false, the identity provider services are hosted within the Session.Proxy process.
    /// </summary>
    public bool RunIdentityProviderSidecar { get; set; } = false;
}
