using System.ComponentModel;
using System.Security.Authentication;
using Agents.Core.Helpers;
using Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Internal.AntiSSRF;
using Microsoft.SemanticKernel;

namespace Agents.Core.Plugins;

public class TlsPlugin
{

    private readonly ILogger<TlsPlugin> _logger;
    private readonly AntiSSRFPolicy _policy;

    public TlsPlugin(ILogger<TlsPlugin> logger)
    {
        _logger = logger;

        _policy = new AntiSSRFPolicy();
        _policy.SetDefaults();
    }

    [KernelFunction("get_tls_settings")]
    [Description("Get the minimum TLS version for a given list of App Service resources")]
    public async Task<List<TlsStatus>> GetTlsSettings(
        [Description("List of resource IDs to check the TLS minimum version for")]
            List<string> resourceIds)
    {
        return await ArmHelper.GetTlsSettings(resourceIds);
    }

    [KernelFunction("validate_connection")]
    [Description("Validate a connection to a specified URL. Specify what version of TLS you used.")]
    public async Task<string> ValidateTLSAsync(
        [Description("The URL to get from")] string url,
        [Description("The TLS protocol version to enforce")] SslProtocols tlsVersion = SslProtocols.Tls12
    )
    {
        _logger.LogInformation($"Preparing to send payload using {tlsVersion}");


        if (URIValidate.IsNonroutableNetworkAddress(url, _policy))
        {
            throw new Exception("The client URI resolves to a nonroutable network address");
        }

        using var handler = new HttpClientHandler
        {
            SslProtocols = tlsVersion,
        };

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Either of these calls could throw if TLS fails or if the call is not successful
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation($"Done validating TLS");

        return "Success";
    }
}

