using System.ComponentModel;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Kusto.Cloud.Platform.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins
{
    public partial class KustoPlugin : IKustoPlugin
    {
        private readonly ILogger<KustoPlugin> _logger;
        private readonly KustoServiceClientFactory _kustoServiceFactory;
        private readonly KustoSettings _kustoSettings;

        private IDictionary<string, KustoConfig> _regionalConfigs;

        [GeneratedRegex("[^a-zA-Z\\d]")]
        private static partial Regex RegionNormalizationRegex();

        public KustoPlugin(ILogger<KustoPlugin> logger, KustoServiceClientFactory kustoServiceFactory, IOptions<KustoSettings> kustoSettings, IOptions<KustoClusterSettings> kustoClusterSettings)
        {
            _logger = logger;
            _kustoServiceFactory = kustoServiceFactory;
            _kustoSettings = kustoSettings.Value;

            _regionalConfigs = new Dictionary<string, KustoConfig> { };

            foreach (var cluster in kustoClusterSettings.Value)
            {
                _regionalConfigs[cluster.Region] = new KustoConfig
                {
                    ClusterUri = cluster.ClusterUri,
                    DatabaseName = cluster.Database,
                    AuthType = _kustoSettings.AuthenticationType,
                    Authority = _kustoSettings.Authority,
                    AuthorityHost = _kustoSettings.AuthorityHost,
                    ApplicationClientId = _kustoSettings.ApplicationClientId,
                    ApplicationCertificate = _kustoSettings.ApplicationCertificate,
                    ManagedIdentityClientId = _kustoSettings.ManagedIdentityClientId,
                };
            }
        }

        [KernelFunction("execute_kusto_query")]
        [Description("Execute a query on the regional kusto cluster")]
        public async Task<string> ExecuteKustoQuery(
            [Description("The region of the target kusto")] string region,
            [Description("The query to execute")] string query)
        {
            _logger.LogInformation($"execute_kusto_query called with {region} / {query}");
            var normalizedRegion = RegionNormalizationRegex().Replace(region, string.Empty).ToLowerInvariant();
            if (!_regionalConfigs.ContainsKey(normalizedRegion))
            {
                throw new ArgumentException($"Invalid region {region}");
            }
            var kustoService = _kustoServiceFactory.CreateKustoService(_regionalConfigs[normalizedRegion]);

            var reader = await kustoService.PerformQueryAsync(query);
            var writer = new StringWriter();

            reader.WriteAsJson(writer, 1024 * 1024, out var size);

            _logger.LogInformation($"result: {writer.ToString()}");
            return writer.ToString();
        }
    }
}
