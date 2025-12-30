// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Data;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Agent.Core;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Data.DataModels;
using Agent.Data.Tools;
using Agent.Plugins.Interface;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Kusto;

public class SamplingOptions
{
    public string Technique { get; set; } = "none"; // e.g., "top", "percent", "interval"
    public int? TopN { get; set; }
    public int? SamplePercent { get; set; }
    public int? IntervalMinutes { get; set; }

    public bool HasValue =>
        !string.IsNullOrEmpty(Technique) && Technique != "none" &&
        (TopN.HasValue || SamplePercent.HasValue || IntervalMinutes.HasValue);
}

public static class SamplingParameterHelper
{
    public static void AddSamplingParameters(
        Dictionary<string, string> parameters,
        SamplingOptions? sampling)
    {
        if (sampling == null || !sampling.HasValue)
        {
            return;
        }

        if (!string.IsNullOrEmpty(sampling.Technique))
        {
            parameters["samplingTechnique"] = sampling.Technique;
        }

        if (sampling.TopN.HasValue)
        {
            parameters["top"] = sampling.TopN.Value.ToString();
        }

        if (sampling.SamplePercent.HasValue)
        {
            parameters["samplePercent"] = sampling.SamplePercent.Value.ToString();
        }

        if (sampling.IntervalMinutes.HasValue)
        {
            parameters["intervalMinutes"] = sampling.IntervalMinutes.Value.ToString();
        }
    }
}

[AgentToolPlugin(IsFirstPartyOnly = true, Category = ToolCategories.LogQuery)]
public partial class KustoPlugin : IKustoPlugin
{
    private readonly ILogger<KustoPlugin> _logger;
    private readonly KustoClient _kustoClient;
    private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;
    private const int TokenLimit = 200000;

    [GeneratedRegex("[^a-zA-Z\\d]")]
    private static partial Regex RegionNormalizationRegex();

    public KustoCluster GetCluster(string region, string groupName)
    {
        var group = _kustoClient.KustoSettings.RegionalClusterGroups?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(groupName))
        {
            group = _kustoClient.KustoSettings.RegionalClusterGroups?.FirstOrDefault(c => string.Equals(c.Name, groupName, StringComparison.OrdinalIgnoreCase));
        }

        if (group == null)
        {
            throw new InvalidOperationException($"Kusto group '{groupName}' not found in the settings.");
        }

        var cluster = group.Regions
            .FirstOrDefault(r => string.Equals(r.Region, region, StringComparison.OrdinalIgnoreCase));

        if (cluster == null)
        {
            throw new InvalidOperationException($"Region '{region}' is not configured in Kusto settings for group '{groupName}'.");
        }

        return cluster;
    }

    public KustoPlugin(ILogger<KustoPlugin> logger, KustoClient kustoClient, IAgentOutboundCommunicationService agentOutboundCommunicationService)
    {
        _logger = logger;
        _kustoClient = kustoClient;
        _agentOutboundCommunicationService = agentOutboundCommunicationService;
    }

    [KernelFunction("execute_kusto_query_on_cluster")]
    [Description("Executes a fully qualified Kusto query on a specific cluster and database, returning the result in JSON format.")]
    public async Task<string> ExecuteClusterKustoQuery(
        [Description("The short name of the target Kusto cluster (without URL schema or suffix).")] string cluster,
        [Description("The name of the target Kusto database.")] string database,
        [Description("The full Kusto query to execute.")] string fullQuery,
        [Description("Whether to print the result.")] bool? printQuery = true,
        [Description("The name of the query tool.")] string toolName = ""
        )
    {
        var queryResult = await ExecuteClusterKustoQueryInternal(cluster, database, fullQuery);
        if (printQuery == true && queryResult.Message != null)
        {
            ChatMessage msg;
            if (!string.IsNullOrEmpty(toolName))
            {
                msg = new ChatMessage(ChatRole.Tool, $"`{toolName}`{Environment.NewLine + Environment.NewLine}{queryResult.Message?.Text}");
            }
            else
            {
                msg = queryResult.Message;
            }

            await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(ToolStatic.AsyncLocalThreadId.Value, msg);
        }

        return queryResult.Result;
    }

    [KernelFunction("execute_kusto_query")]
    [Description("Executes a Kusto query on a regional cluster using a typed Azure region enum and returns the result as a JSON string. Preferred method for type safety.")]
    public async Task<string> ExecuteKustoQuery(
        [Description("The Azure region of the target Kusto cluster.")] AzureRegion region,
        [Description("The Kusto query to execute.")] string query,
        [Description("Optional group name for the Kusto cluster.")] string? groupName = null
        )
    {
        return (await ExecuteKustoQueryInternal(region, query, groupName)).Result;
    }

    public async Task<KustoQueryResult> ExecuteKustoQueryInternal(
        AzureRegion region,
        string query,
        string? groupName
        )
    {
        KustoCluster? cluster = null;
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Region and query must be provided.");
            }
            if (_kustoClient.KustoSettings.RegionalClusterGroups.Count == 0)
            {
                _logger.LogInternalError("No regional clusters are configured in Kusto settings.");
                throw new InvalidOperationException("No regional clusters are configured in Kusto settings.");
            }
            cluster = GetCluster(region.ToNormalizedString(), groupName ?? string.Empty);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInternalInformation($"execute_kusto_query called with {region} / {query}");

            var normalizedRegion = RegionNormalizationRegex().Replace(region.ToNormalizedString(), string.Empty).ToLowerInvariant();
            using var reader = await _kustoClient.PerformQueryAsync(cluster.ClusterUri ?? string.Empty, cluster.Database ?? string.Empty, query);

            stopwatch.Stop();
            var ret = new KustoQueryResult(reader, query);
            ret.Message = CreateChatMessage(query, normalizedRegion, ret.RowCount, (int)stopwatch.ElapsedMilliseconds, groupName: groupName ?? string.Empty);

            return ret;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"An error occurred while executing Kusto Query: {ex.Message}");
            return CreateErrorResult(query, ex.Message, region.ToNormalizedString(), cluster?.Database, null, groupName);
        }
    }

    internal static string GetKqlFilePath(string functionName, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(functionName))
        {
            return string.Empty;
        }

        var baseDir = Path.Combine(baseDirectory, "Plugins", "Definitions", "Queries");

        // Maintain existing behavior: direct file name search (backward compatibility)
        var directPath = Path.Combine(baseDir, $"{functionName}.kql");
        if (File.Exists(directPath))
        {
            return directPath;
        }

        // New feature: namespace format only
        if (functionName.Contains('.'))
        {
            var parts = functionName.Split('.');
            var kqlFileName = parts.Last() + ".kql";
            var subDirs = parts.Take(parts.Length - 1).ToArray();

            var namespacedFile = Path.Combine(new[] { baseDir }.Concat(subDirs).Concat(new[] { kqlFileName }).ToArray());
            if (File.Exists(namespacedFile))
            {
                return namespacedFile;
            }
        }

        // Return the same path as existing behavior (File.Exists check is done by caller)
        return directPath;
    }

    [KernelFunction("list_kusto_functions")]
    [Description("Lists all available user-defined functions from the Kusto cluster for a given region using string, including metadata like name, folder, and description. Use enum-based version for better type safety.")]
    public async Task<List<KustoFunctionInfo>> ListFunctionsAsync(
        [Description("The region of the Kusto cluster to query.")] AzureRegion region)
    {
        var query = ".show functions | project Name, Folder, DocString, Parameters";
        var result = new List<KustoFunctionInfo>();
        var cluster = GetCluster(region.ToNormalizedString(), string.Empty);
        using var reader = await _kustoClient.PerformQueryAsync(cluster.ClusterUri ?? string.Empty, cluster.Database ?? string.Empty, query);
        while (reader.Read())
        {
            result.Add(new KustoFunctionInfo
            {
                Name = reader["Name"]?.ToString() ?? string.Empty,
                Folder = reader["Folder"]?.ToString() ?? string.Empty,
                DocString = reader["DocString"]?.ToString() ?? string.Empty,
                Parameters = reader["Parameters"]?.ToString() ?? string.Empty
            });
        }

        return result;
    }

    [KernelFunction("execute_kusto_function")]
    [Description("Executes a user-defined Kusto function with named arguments on the regional Kusto cluster using AzureRegion region and returns the results. Use enum-based version for better type safety.")]
    public async Task<string> ExecuteFunctionAsync(
        [Description("The name of the Kusto function to invoke.")] string functionName,
        [Description("The region of the Kusto cluster.")] AzureRegion region,
        Dictionary<string, string>? args = null,
        [Description("Optional group name for the Kusto cluster.")] string? groupName = null)
    {
        return (await ExecuteFunctionInternalAsync(functionName, region, args, groupName)).Result;
    }

    public async Task<KustoQueryResult> ExecuteFunctionInternalAsync(
        string functionName,
        AzureRegion region,
        Dictionary<string, string>? args = null,
        string? groupName = null)
    {
        var argList = args != null && args.Count > 0
            ? string.Join(", ", args.Select(kvp => $"{kvp.Key}={QuoteIfNeeded(kvp.Value)}"))
            : "";

        var query = string.IsNullOrEmpty(argList) ? $"{functionName}()" : $"{functionName}({argList})";

        KustoCluster? cluster = null;
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            cluster = GetCluster(region.ToNormalizedString(), groupName ?? string.Empty);
            using var reader = await _kustoClient.PerformQueryAsync(cluster.ClusterUri ?? string.Empty, cluster.Database ?? string.Empty, query);
            var ret = new KustoQueryResult(reader, query);
            stopwatch.Stop();
            ret.Message = CreateChatMessage(query, region.ToNormalizedString(), ret.RowCount, (int)stopwatch.ElapsedMilliseconds, functionName: functionName, groupName: groupName ?? string.Empty);
            return ret;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"An error occurred while executing Kusto Function {functionName}: {ex.Message}");
            return CreateErrorResult(query, ex.Message, region.ToNormalizedString(), cluster?.Database, functionName, groupName);
        }
    }

    private static string QuoteIfNeeded(string value)
    {
        return $"\"{value}\"";
    }

    private (string adxUri, string database) BuildAdxUriWithDatabase(string query, string regionOrClusterUri, string? database = null, string? groupName = null)
    {
        var adxUri = regionOrClusterUri;
        var resultDatabase = database ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(regionOrClusterUri))
        {
            // Supplied parameter is a cluster URI
            adxUri = regionOrClusterUri;
            if (string.IsNullOrWhiteSpace(database))
            {
                resultDatabase = string.Empty;
            }
        }

        if (!string.IsNullOrEmpty(adxUri))
        {
            adxUri = adxUri.Replace(".kusto.windows.net", "");
            adxUri = adxUri.Replace("https://", "");
        }

        var encodedQuery = EncodeQuery(query);
        var fullAdxUri = $"https://dataexplorer.azure.com/clusters/{adxUri}/{resultDatabase}?query={encodedQuery}";
        return (fullAdxUri, resultDatabase);
    }

    private string BuildDisplayText(string adxUri, string query, string regionOrClusterUri, string database, string? functionName = null, string? additionalInfo = null, bool isError = false)
    {
        var prefix = isError ? "Error " : "";
        var verb = isError ? "executing" : "Executed";

        if (!string.IsNullOrWhiteSpace(functionName))
        {
            // For function execution
            var functionInfo = isError ? $"{prefix}{verb.ToLowerInvariant()} function `{functionName}`" : $"{verb} function `{functionName}` against {regionOrClusterUri}{(!string.IsNullOrWhiteSpace(database) ? $"/{database}" : string.Empty)}";
            var errorPart = isError ? $"<strong>{functionInfo}:</strong> {additionalInfo}" : functionInfo;
            return $"[Execute in ADX]({adxUri})\n\n{errorPart}:\n<details><summary>View KQL Query</summary>\n<pre>\n{query}\n</pre>\n\n</details>{(isError ? "" : $"\n{additionalInfo}")}";
        }
        else if (!string.IsNullOrWhiteSpace(database))
        {
            // For cluster and database-specific queries
            var queryInfo = isError ? $"{prefix}{verb.ToLowerInvariant()} query on cluster '{regionOrClusterUri}' in database '{database}'" : $"{verb} query on cluster '{regionOrClusterUri}' in database '{database}'";
            var errorPart = isError ? $"<strong>{queryInfo}:</strong> {additionalInfo}" : queryInfo;
            return $"[Execute in ADX]({adxUri})\n\n{errorPart}:\n<details><summary>View KQL Query</summary>\n<pre>\n{query}\n</pre>\n\n</details>{(isError ? "" : $"\n{additionalInfo}")}";
        }
        else
        {
            // For regional queries
            var queryInfo = isError ? $"{prefix}{verb.ToLowerInvariant()} query in region {regionOrClusterUri}" : $"{verb} query in region {regionOrClusterUri}";
            var errorPart = isError ? $"<strong>{queryInfo}:</strong> {additionalInfo}" : queryInfo;
            return $"[Execute in ADX]({adxUri})\n\n{errorPart}:\n<details><summary>View KQL Query</summary>\n<pre>\n{query}\n</pre>\n\n</details>{(isError ? "" : $"\n{additionalInfo}")}";
        }
    }

    public ChatMessage CreateChatMessage(string query, string regionOrClusterUri, int count, int queryExecutionTimeInMilliSeconds, string? database = null, string? functionName = null, string? groupName = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (string.IsNullOrWhiteSpace(regionOrClusterUri))
        {
            throw new ArgumentNullException(regionOrClusterUri, nameof(regionOrClusterUri));
        }

        // Special validation for cluster URIs (maintaining original behavior)
        if (regionOrClusterUri.IndexOf(".kusto.", StringComparison.OrdinalIgnoreCase) >= 0 && string.IsNullOrWhiteSpace(database))
        {
            throw new ArgumentNullException(nameof(database), "Database name is required when using a full cluster URI.");
        }

        var (adxUri, resultDatabase) = BuildAdxUriWithDatabase(query, regionOrClusterUri, database, groupName);

        var executionTime = $"{queryExecutionTimeInMilliSeconds / 1000.0} secs";
        var additionalInfo = $"Rows: {count} Execution time: {executionTime}";
        var displayText = BuildDisplayText(adxUri, query, regionOrClusterUri, resultDatabase, functionName, additionalInfo, isError: false);

        return new ChatMessage(ChatRole.Tool, new List<AIContent>
            {
            new UriContent(adxUri, "text/html"),
            new Microsoft.Extensions.AI.TextContent(displayText)
        });
    }

    public static string EncodeQuery(string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
        {
            gzipStream.Write(inputBytes, 0, inputBytes.Length);
        }
        var compressedBytes = outputStream.ToArray();
        var base64 = Convert.ToBase64String(compressedBytes);
        var urlEncoded = HttpUtility.UrlEncode(base64);
        return urlEncoded;
    }

    private KustoQueryResult CreateErrorResult(string query, string errorMessage, string? regionOrCluster = null, string? database = null, string? functionName = null, string? groupName = null)
    {
        var fullErrorMessage = functionName != null
            ? $"An error occurred while executing Kusto Function {functionName}: {errorMessage}"
            : $"An error occurred while executing Kusto Query: {errorMessage}";

        ChatMessage message;
        if (!string.IsNullOrWhiteSpace(regionOrCluster))
        {
            // Create message with ADX link using same logic as CreateChatMessage
            try
            {
                var (fullAdxUri, resultDatabase) = BuildAdxUriWithDatabase(query, regionOrCluster, database, groupName);
                var displayText = BuildDisplayText(fullAdxUri, query, regionOrCluster, resultDatabase, functionName, errorMessage, isError: true);

                message = new ChatMessage(ChatRole.Tool, new List<AIContent>
                {
                    new UriContent(fullAdxUri, "text/html"),
                    new Microsoft.Extensions.AI.TextContent(displayText)
                });
            }
            catch
            {
                // Fallback to simple message if ADX URI construction fails
                message = new ChatMessage(ChatRole.Tool, $"<details><summary>View KQL Query</summary>\n<pre>\n{query}\n</pre>\n\n</details>\n\n<strong>{fullErrorMessage}</strong>");
            }
        }
        else
        {
            // Fallback to simple message when region/cluster info is not available
            message = new ChatMessage(ChatRole.Tool, $"<details><summary>View KQL Query</summary>\n<pre>\n{query}\n</pre>\n\n</details>\n\n<strong>{fullErrorMessage}</strong>");
        }

        return new KustoQueryResult()
        {
            Success = false,
            Query = query,
            Result = fullErrorMessage,
            Message = message,
            RowCount = 0,
        };
    }

    internal static string GetKqlFilePath(string functionName)
    {
        return GetKqlFilePath(functionName, AppContext.BaseDirectory);
    }

    private static string FormatQuery(Dictionary<string, string> args, string fileName)
    {
        var formatted = File.ReadAllText(fileName);
        return FormatTemplate(formatted, args);
    }

    public virtual async Task<KustoQueryResult> ExecuteClusterKustoQueryInternal(
        string cluster,
        string database,
        string fullQuery)
    {
        // Normalize the cluster URL
        cluster = cluster.Replace("https://", "").Replace("http://", "");

        // Only append .kusto.windows.net if the cluster doesn't already have a domain suffix
        string clusterUrl;
        if (cluster.Contains('.'))
        {
            // Cluster is already a FQDN (e.g., kusto.aria.microsoft.com or mycluster.kusto.windows.net)
            clusterUrl = $"https://{cluster}";
        }
        else
        {
            // Cluster is just a name, append the default suffix
            clusterUrl = $"https://{cluster}.kusto.windows.net";
        }

        var logMessage = $"[execute_kusto_query_on_cluster][{DateTime.UtcNow}] Invoked with cluster: {cluster}, database: {database}\nquery:\n{fullQuery.Substring(0, Math.Min(100, fullQuery.Length))}...";
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var reader = await _kustoClient.PerformQueryAsync(clusterUrl, database, fullQuery);
            var result = new KustoQueryResult(reader, fullQuery);
            stopwatch.Stop();
            if (result.Result != null && result.Result != string.Empty)
            {
                if (result.RowCount == 0 && !result.Result.StartsWith("An error occurred while executing Kusto Query"))
                {
                    result.Result = "ZERO_ROWS_RETURNED";
                }
            }
            else
            {
                _logger.LogInternalInformation($"Kusto query execution failed. Result: {result?.Result}, Message: {result?.Message}");
                result = CreateErrorResult(fullQuery, "Kusto query execution failed.", cluster, database);
            }
            result.Message = CreateChatMessage(fullQuery, cluster, result.RowCount, (int)stopwatch.ElapsedMilliseconds, database);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"An error occurred while executing Kusto Query: {ex.Message}");
            return CreateErrorResult(fullQuery, ex.Message, cluster, database);
        }
    }

    /// <summary>
    /// Formats a string by substituting both ##parameter## and $parameterName patterns
    /// with corresponding values from the args dictionary.
    /// </summary>
    /// <param name="template">The input string template containing placeholders.</param>
    /// <param name="args">Dictionary of parameter names and their values.</param>
    /// <returns>The formatted string with parameters substituted.</returns>
    public static string FormatTemplate(string template, Dictionary<string, string> args)
    {
        if (string.IsNullOrEmpty(template)
            || args == null
            || args.Count == 0)
        {
            return template;
        }

        var formatted = template;

        // Replace longer keys first to avoid overlap issues
        foreach (var arg in args.OrderByDescending(kvp => kvp.Key.Length))
        {
            formatted = formatted
                .Replace($"##{arg.Key}##", arg.Value)
                .Replace($"${arg.Key}", arg.Value);
        }

        return formatted;
    }

    // Single method: optional displayOptions keeps existing behavior when null
    public async Task<string> ExecuteLocalFunctionOnClusterAsync(
        string functionName,
        string clusterName,
        string databaseName,
        Dictionary<string, string> args,
        KustoDisplayOptions? displayOptions = null,
        KustoToolDefinition? toolDefinition = null)
    {
        var fileName = GetKqlFilePath(functionName);

        string displayFunctionName;
        if (!string.IsNullOrWhiteSpace(toolDefinition?.Function))
        {
            displayFunctionName = toolDefinition.Function;
        }
        else if (!string.IsNullOrWhiteSpace(toolDefinition?.Name))
        {
            displayFunctionName = toolDefinition.Name;
        }
        else
        {
            displayFunctionName = functionName;
        }

        KustoQueryResult queryResult;
        if (File.Exists(fileName))
        {
            var formatted = FormatQuery(args, fileName);
            queryResult = await ExecuteClusterKustoQueryInternal(clusterName, databaseName, formatted);
        }
        else
        {
            var fallbackQuery = toolDefinition?.Query;
            if (string.IsNullOrWhiteSpace(fallbackQuery))
            {
                throw new ArgumentException($"Function {functionName} not found in {fileName}");
            }

            _logger.LogInternalWarning(
                "KQL file for function {FunctionName} not found at {FileName}. Falling back to inline query from tool definition.",
                functionName,
                fileName);

            var formattedFallback = FormatTemplate(fallbackQuery!, args);
            queryResult = await ExecuteClusterKustoQueryInternal(clusterName, databaseName, formattedFallback);
        }

        if (queryResult.Result.Length > TokenLimit)
        {
            return "Query result row count is over thersholds a user should use sampling";
        }

        ChatMessage msg;
        if (displayOptions is { } opts && queryResult.Message != null)
        {
            var enhanced = KustoDisplayFormatter.BuildDisplayMessage(queryResult.Message, queryResult.Result, opts);
            msg = new ChatMessage(ChatRole.Tool, $"`{displayFunctionName}`{Environment.NewLine + Environment.NewLine}{enhanced.Text}");
        }
        else
        {
            msg = new ChatMessage(ChatRole.Tool, $"`{displayFunctionName}`{Environment.NewLine + Environment.NewLine}{queryResult.Message?.Text}");
        }

        await HandleKustoResult(msg);

        return queryResult.Result;
    }

    public async Task<string> ExecuteLocalFunctionAsync(string functionName, AzureRegion region, Dictionary<string, string> args, string? groupName, SamplingOptions? samplingOptions = null)
    {
        SamplingParameterHelper.AddSamplingParameters(args, samplingOptions);
        var fileName = GetKqlFilePath(functionName);
        KustoQueryResult queryResult;

        if (File.Exists(fileName))
        {
            var formatted = FormatQuery(args, fileName);
            queryResult = await ExecuteKustoQueryInternal(region, formatted, groupName ?? string.Empty);
        }
        else
        {
            queryResult = await ExecuteFunctionInternalAsync(functionName, region, args, groupName ?? string.Empty);
        }

        if (queryResult.Result.Length > TokenLimit)
        {
            return "Query result row count is over thersholds a user should use sampling";
        }

        var msg = new ChatMessage(ChatRole.Tool, $"`{functionName}`{Environment.NewLine + Environment.NewLine}{queryResult.Message?.Text}");
        await HandleKustoResult(msg);

        return queryResult.Result;
    }

    /// <summary>
    /// Handles Kusto result output routing based on context.
    /// Routes to agent task storage if in agent task context, otherwise to normal chat flow.
    /// </summary>
    private async Task HandleKustoResult(ChatMessage msg)
    {
        // Check if we're in agent task context and route accordingly
        var agentTaskId = Agent.Core.ToolStatic.AsyncLocalAgentTaskId.Value;
        var threadId = ToolStatic.AsyncLocalThreadId.Value;

        if (agentTaskId.HasValue)
        {
            // Agent task context - use dedicated handler with same content as chat message
            await _agentOutboundCommunicationService.HandleAgentTaskKustoResult(
                threadId,
                msg.Text ?? string.Empty);
        }
        else
        {
            // Normal chat flow - use existing method
            await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                threadId, msg);
        }
    }
}
