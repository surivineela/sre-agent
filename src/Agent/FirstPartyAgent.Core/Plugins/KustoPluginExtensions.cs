using FirstPartyAgent.Plugins;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.Plugins;
public static class KustoPluginExtensions
{
    public static Task<string> ExecuteLocalFunctionAsync(this IKustoPlugin _kustoPlugin, ILogger _logger, string functionName, string region, Dictionary<string, string> args)
    {
        var fileName = Path.Combine(AppContext.BaseDirectory, "Plugins", "Definitions", "Queries", $"{functionName}.kql");

        if (File.Exists(fileName))
        {
            var formatted = File.ReadAllText(fileName);
            // replace ##placeholder## with value
            foreach (var arg in args)
            {
                formatted = formatted.Replace($"##{arg.Key}##", arg.Value);
            }

            if (formatted.Contains("##"))
            {
                _logger.LogError($"Not all placeholders were replaced in the query");
                throw new Exception($"Not all placeholders were replaced in the query, {formatted}");
            }

            return _kustoPlugin.ExecuteKustoQuery(region, formatted);
        }
        else
        {
            return _kustoPlugin.ExecuteFunctionAsync(functionName, region, args);
        }
    }
}
