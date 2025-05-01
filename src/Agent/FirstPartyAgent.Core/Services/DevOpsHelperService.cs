using System.Text.Json;
using System.Text.RegularExpressions;
using FirstPartyAgent.Core.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.Services;
public class DevOpsHelperService
{
    private readonly ILogger<DevOpsHelperService> _logger;
    private readonly DevOpsSetting _devOpsSetting;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpClientFactory;
    public DevOpsHelperService(ILogger<DevOpsHelperService> logger, DevOpsSetting devOpsSetting, IWebHostEnvironment env, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _devOpsSetting = devOpsSetting;
        _env = env;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> FetchAdoTsg(string url)
    {
        try
        {
            string pattern = @"https://(?<org>[^.]+)\.visualstudio\.com/(?<project>[^/]+)/_wiki/wikis/(?<wikiId>[^/]+)/(?<id>\d+)/";
            var match = Regex.Match(url, pattern);

            if (!match.Success)
            {
                throw new ArgumentException("Invalid Azure Devops Wiki URL format");
            }

            string org = match.Groups["org"].Value;
            string project = match.Groups["project"].Value;
            string wikiId = match.Groups["wikiId"].Value;
            string id = match.Groups["id"].Value;

            var httpClient = _httpClientFactory.CreateClient(nameof(DevOpsHelperService));


            var response = await httpClient.GetAsync($"https://dev.azure.com/{org}/{project}/_apis/wiki/wikis/{wikiId}/pages?id={id}&includeContent=True");
            var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

            // read json["content"] as string
            string result = json.GetProperty("content").GetString();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch ADO TSG");
            return string.Empty;
        }

    }
}
