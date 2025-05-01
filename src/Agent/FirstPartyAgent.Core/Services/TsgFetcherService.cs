using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.Services;
public class TsgFetcherService
{
    private readonly ILogger<TsgFetcherService> _logger;
    private readonly DevOpsHelperService _devOpsHelperService;
    public TsgFetcherService(ILogger<TsgFetcherService> logger, DevOpsHelperService devOpsHelperService)
    {
        _logger = logger;
        _devOpsHelperService = devOpsHelperService;
    }

    public async Task<string> Fetch(string url)
    {
        var dict = new Dictionary<string, Func<string, Task<string>>>
        {
            [@"https:\/\/.+\.visualstudio\.com\/.+\/_wiki/wikis"] = _devOpsHelperService.FetchAdoTsg
        };

        foreach (var kvp in dict)
        {
            if (Regex.IsMatch(url, kvp.Key))
            {
                return await kvp.Value(url);
            }
        }

        throw new ArgumentException($"Url {url} is not supported.");
    }
}
