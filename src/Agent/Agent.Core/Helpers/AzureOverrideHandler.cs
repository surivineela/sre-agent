using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Core.Helpers;

public class AzureOverrideHandler : DelegatingHandler
{
    private readonly string _overrideApiVersion;

    public AzureOverrideHandler(string overrideApiVersion)
    {
        _overrideApiVersion = overrideApiVersion;
        InnerHandler = new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content != null && request.RequestUri != null)
        {
            string requestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            if ((requestBody.Contains("\"model\":\"o1", StringComparison.Ordinal) ||
                 requestBody.Contains("\"model\":\"o3", StringComparison.Ordinal)) &&
                requestBody.Contains("\"max_tokens\":", StringComparison.Ordinal))
            {
                requestBody = requestBody.Replace("\"max_tokens\":", "\"max_completion_tokens\":");
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            }
        }

        if (request.RequestUri != null)
        {
            string requestUriStr = request.RequestUri.ToString();
            var currentVersion = Regex.Match(requestUriStr, @"\d{4}-\d{2}-\d{2}(-preview)?$").Value;

            if (!string.IsNullOrEmpty(currentVersion))
            {
                requestUriStr = requestUriStr.Replace(currentVersion, _overrideApiVersion);
            }
            else
            {
                string separator = requestUriStr.Contains("?") ? "&" : "?";
                requestUriStr = $"{requestUriStr}{separator}api-version={_overrideApiVersion}";
            }

            request.RequestUri = new Uri(requestUriStr);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
