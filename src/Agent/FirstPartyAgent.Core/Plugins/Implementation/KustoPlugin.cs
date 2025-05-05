// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Agent.Core;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Services;
using Kusto.Cloud.Platform.Data;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins
{
    public partial class KustoPlugin : IKustoPlugin
    {
        private readonly ILogger<KustoPlugin> _logger;
        private readonly KustoClientService _kustoClientService;
        private readonly ITeamsClient _teamsClient;
        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;

        [GeneratedRegex("[^a-zA-Z\\d]")]
        private static partial Regex RegionNormalizationRegex();

        public KustoPlugin(ILogger<KustoPlugin> logger, KustoClientService kustoClientService, ITeamsClient teamsClient, IAgentOutboundCommunicationService agentOutboundCommunicationService)
        {
            _teamsClient = teamsClient;
            _agentOutboundCommunicationService = agentOutboundCommunicationService;
            _logger = logger;
            _kustoClientService = kustoClientService;
        }

        public static string EncodeQuery(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            using (var outputStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
                {
                    gzipStream.Write(inputBytes, 0, inputBytes.Length);
                }
                byte[] compressedBytes = outputStream.ToArray();
                string base64 = Convert.ToBase64String(compressedBytes);
                string urlEncoded = HttpUtility.UrlEncode(base64);
                return urlEncoded;
            }
        }

        [KernelFunction("execute_kusto_query")]
        [Description("Executes a Kusto query on a regional cluster and returns the result as a JSON string.")]
        public async Task<string> ExecuteKustoQuery(
            [Description("The region of the target Kusto cluster.")] string region,
            [Description("The Kusto query to execute.")] string query,
            bool displayQuery = true)
        {
            try
            {
                _logger.LogInformation($"execute_kusto_query called with {region} / {query}");
                if (displayQuery)
                {
                    var cluster = _kustoClientService.GetCluster(region);
                    var uriWithoutHttps = cluster.ClusterUri.Replace("https://", "");
                    var adxUri = $"https://dataexplorer.azure.com/clusters/{uriWithoutHttps}/{cluster.Database}?query={EncodeQuery(query)}";
                    var msg = new ChatMessage(ChatRole.Tool, new List<AIContent>()
                    {
                        new UriContent(adxUri, "text/html"),
                        new Microsoft.Extensions.AI.TextContent($"[adx]({adxUri})\nExecuting Kusto query in region {region}:\n```kql\n{query}\n```"),
                    });

                    await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(ToolStatic.AsyncLocalThreadId.Value, string.Empty, msg);
                }

                var normalizedRegion = RegionNormalizationRegex().Replace(region, string.Empty).ToLowerInvariant();
                var reader = await _kustoClientService.PerformQueryAsync(query, region);
                var writer = new StringWriter();

                reader.WriteAsJson(writer, 1024 * 1024, out var size);

                _logger.LogInformation($"result: {writer.ToString()}");
                return writer.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while executing Kusto Query: {ex.Message}");
                return $"An error occurred while executing Kusto Query: {ex.Message}";
            }
        }

        [KernelFunction("execute_kusto_query_on_cluster")]
        [Description("Executes a fully qualified Kusto query on a specific cluster and database, returning the result in JSON format.")]
        public async Task<string> ExecuteClusterKustoQuery(
            [Description("The short name of the target Kusto cluster (without URL schema or suffix).")] string cluster,
            [Description("The name of the target Kusto database.")] string database,
            [Description("The full Kusto query to execute.")] string fullQuery,
            DateTime? NowOverride,
            Kernel kernel,
            bool displayQuery = true)
        {
            cluster = cluster.Replace(".kusto.windows.net", "");
            cluster = cluster.Replace("https://", "");

            if (displayQuery)
            {
                var adxUri = $"https://dataexplorer.azure.com/clusters/{cluster}.kusto.windows.net/databases/{database}?query={EncodeQuery(fullQuery)}";
                var chatMessage = new List<AIContent>()
                {
                    new UriContent(adxUri, "text/html"),
                    new Microsoft.Extensions.AI.TextContent($"<code>{fullQuery}</code>"),
                };
                await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(ToolStatic.AsyncLocalThreadId.Value, string.Empty, new ChatMessage(ChatRole.Tool, chatMessage));
            }

            var logMessage = $"[execute_kusto_query_on_cluster][{DateTime.UtcNow}] Invoked with cluster: {cluster}, database: {database}\nquery:\n{fullQuery.Substring(0, Math.Min(100, fullQuery.Length))}...";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            try
            {
                var config = new KustoCluster
                {
                    ClusterUri = $"https://{cluster}.kusto.windows.net",
                    Database = database,
                };
                var reader = await _kustoClientService.PerformQueryAsync(config, fullQuery);
                var writer = new StringWriter();

                reader.WriteAsJson(writer, 1024 * 1024, out var size);
                if (size > 1024 * 1024)
                {
                    _logger.LogWarning($"Kusto query result size exceeds 1MB: {size} bytes");
                }
                var result = writer.ToString();
                _logger.LogInformation($"Kusto Output: {result}");

                return !string.IsNullOrWhiteSpace(result) ? result : "ZERO_ROWS_RETURNED";
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while executing Kusto Query: {ex.Message}");
                return $"An error occurred while executing Kusto Query: {ex.Message}";
            }
        }

        [KernelFunction("list_kusto_functions")]
        [Description("Lists all available user-defined functions from the Kusto cluster for a given region, including metadata like name, folder, and description.")]
        public async Task<List<KustoFunction>> ListFunctionsAsync(
            [Description("The region of the Kusto cluster to query.")] string region)
        {
            var query = ".show functions | project Name, Folder, DocString, Parameters";
            var result = new List<KustoFunction>();

            using var reader = await _kustoClientService.PerformQueryAsync(query, region);
            while (reader.Read())
            {
                result.Add(new KustoFunction
                {
                    Name = reader["Name"].ToString(),
                    Folder = reader["Folder"]?.ToString(),
                    DocString = reader["DocString"]?.ToString(),
                    Parameters = reader["Parameters"]?.ToString()
                });
            }

            return result;
        }

        [KernelFunction("execute_kusto_function")]
        [Description("Executes a user-defined Kusto function with named arguments on the regional Kusto cluster and returns the results.")]
        public async Task<string> ExecuteFunctionAsync(
            [Description("The name of the Kusto function to invoke.")] string functionName,
            [Description("The region of the Kusto cluster.")] string region,
            Dictionary<string, string>? args = null,
            bool displayQuery = true)
        {
            string argList = args != null && args.Count > 0
                ? string.Join(", ", args.Select(kvp => $"{kvp.Key}={QuoteIfNeeded(kvp.Value)}"))
                : "";

            var query = string.IsNullOrEmpty(argList) ? $"{functionName}()" : $"{functionName}({argList})";

            if (displayQuery)
            {
                var adxUri = $"https://dataexplorer.azure.com/clusters/{region}.kusto.windows.net/databases/{region}?query={EncodeQuery(query)}";
                var msg = new ChatMessage(ChatRole.Tool, new List<AIContent>()
                {
                    new UriContent(adxUri, "text/html"),
                    new Microsoft.Extensions.AI.TextContent($"[adx]({adxUri})\nExecuting Kusto function `{functionName}` in region {region}:\n```kql\n{query}\n```"),
                });

                await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(ToolStatic.AsyncLocalThreadId.Value, string.Empty, msg);
            }

            using var reader = await _kustoClientService.PerformQueryAsync(query, region);

            var output = new StringBuilder();
            while (reader.Read())
            {
                output.AppendLine(reader[0].ToString());
            }

            return output.ToString();
        }

        private static string QuoteIfNeeded(string value)
        {
            return  $"\"{value}\"" ;
        }

        public async Task<string> ExecuteLocalFunctionAsync(string functionName, string region, Dictionary<string, string> args)
        {
            await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(ToolStatic.AsyncLocalThreadId.Value, string.Empty, new ChatMessage(ChatRole.Tool, $"Performing Kusto Query `{functionName}`"));
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

                return await ExecuteKustoQuery(region, formatted);
            }
            else
            {
                return await ExecuteFunctionAsync(functionName, region, args);
            }
        }
    }
}
