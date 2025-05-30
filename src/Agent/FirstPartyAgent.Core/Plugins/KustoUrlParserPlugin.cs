// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Text.Json;
using FirstPartyAgent.Core.Models;

namespace FirstPartyAgent.Core.Plugins
{
    public class KustoUrlParserPlugin(ILogger<KustoUrlParserPlugin> logger)
    {
        private readonly ILogger<KustoUrlParserPlugin> _logger = logger;

        [KernelFunction("parse_kusto_url")]
        [Description("Parses a Kusto URL and extracts details (Cloud, Cluster, Database, KustoQuery) as a JSON string")]
        public async Task<string> ParseKustoUrl([Description("The Kusto URL to parse")] string kustoUrl)
        {
            try
            {
                var logMessage = $"[parse_kusto_url][{DateTime.UtcNow}] Invoked with kustoUrl: {kustoUrl}";
                _logger.LogInformation(logMessage);

                string? extractedJsonDetails = await Task.Run(() => KustoUrlParser.GetKustoDetailsAsJson(kustoUrl, _logger));

                string responseMessage;
                if (!string.IsNullOrEmpty(extractedJsonDetails))
                {
                    responseMessage = extractedJsonDetails;
                }
                else
                {
                    responseMessage = "{\"error\":\"Could not parse the provided Kusto URL or it is not a valid Kusto URL.\"}";
                }
                
                _logger.LogInformation($"[parse_kusto_url][{DateTime.UtcNow}] Response: {responseMessage}");
                return responseMessage;
            }
            catch (Exception ex)
            {
                var errorMessage = $"An error occurred while parsing the Kusto URL: {ex.Message}";
                _logger.LogError(errorMessage);

                string safeErrorMessage = JsonEncodedText.Encode(errorMessage).ToString();
                return $"{{\"error\":\"{safeErrorMessage}\"}}";
            }
        }


        /// <summary>
        /// Utility class for parsing and extracting information from Kusto Data Explorer URLs across all Azure clouds
        /// </summary>
        public static class KustoUrlParser
        {
            // Centralized regex patterns
            private static readonly Regex KustoUrlPattern = new(
                @"https://(?<cluster>[^\.]+)(?:\.(?<region>[^\.]+))?\.kusto\.(?<domain>windows\.net|usgovcloudapi\.net|chinacloudapi\.cn|cloudapi\.de)(?::(?<port>\d+))?/?(?<path>[^?]*)(?:\?(?<queryString>.*))?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            private static readonly Regex DataExplorerUrlPattern = new(
                @"https://dataexplorer\.(?<domain>azure\.com|azure\.us|azure\.cn|microsoftazure\.de)/(?<path>[^?]*)(?:\?(?<queryString>.*))?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            private static readonly Regex ClusterPathPattern = new(
                @"clusters/([^/]+)(?:/databases/([^/]+))?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            private static readonly Regex ClusterUriPattern = new(
                @"(?:https?://)?([^.]+)\.kusto\.(?<domain>windows\.net|usgovcloudapi\.net|chinacloudapi\.cn|cloudapi\.de)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Common parameter names
            private static readonly string[] QueryParams = ["query", "kql", "kustoQuery", "q"];

            /// <summary>
            /// Extracts structured information from Kusto-related URLs.
            /// This method consolidates the parsing logic for different Kusto URL formats.
            /// </summary>
            /// <param name="url">The URL to parse.</param>
            /// <param name="logger">Optional logger for debugging.</param>
            /// <returns>Extracted Kusto information as ICMConfigKustoQueryModel or null if not a Kusto URL or parsing fails.</returns>
            public static ICMConfigKustoQueryModel? ExtractKustoInfo(string url, ILogger? logger = null)
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
                {
                    logger?.LogDebug($"URL is not a valid URI: {url}");
                    return null;
                }

                try
                {
                    Match directMatch = KustoUrlPattern.Match(url); // Regex still applied to original URL string
                    if (directMatch.Success)
                    {
                        var kustoModel = new ICMConfigKustoQueryModel();
                        var clusterNamePart = directMatch.Groups["cluster"].Value;
                        var regionPart = directMatch.Groups["region"].Value;
                        var domainPart = directMatch.Groups["domain"].Value;

                        kustoModel.Cluster = !string.IsNullOrEmpty(regionPart)
                            ? $"https://{clusterNamePart}.{regionPart}.kusto.{domainPart}"
                            : $"https://{clusterNamePart}.kusto.{domainPart}";
                        kustoModel.Cloud = GetCloudEnvironmentFromDomain(domainPart).ToString();

                        var path = directMatch.Groups["path"].Value;
                        var parameters = ExtractQueryParameters(uri.Query); 

                        if (!string.IsNullOrEmpty(path))
                        {
                            var pathSpan = path.AsSpan();
                            if (pathSpan.Length > 0 && pathSpan[0] == '/') pathSpan = pathSpan.Slice(1);
                            var firstPathSegmentEnd = pathSpan.IndexOf('/');
                            var firstPath = firstPathSegmentEnd == -1 ? pathSpan : pathSpan.Slice(0, firstPathSegmentEnd);
                            if (!firstPath.IsEmpty) kustoModel.Database = firstPath.ToString();
                        }
                        if (string.IsNullOrEmpty(kustoModel.Database) && parameters.TryGetValue("db", out var dbName))
                        {
                            kustoModel.Database = dbName;
                        }

                        foreach (var paramName in QueryParams)
                        {
                            if (parameters.TryGetValue(paramName, out var queryValue) && !string.IsNullOrEmpty(queryValue))
                            {
                                kustoModel.KustoQuery = DecodeKustoQuery(queryValue, logger) ?? string.Empty;
                                break;
                            }
                        }
                        return kustoModel;
                    }

                    Match dataExplorerMatch = DataExplorerUrlPattern.Match(url);
                    if (dataExplorerMatch.Success)
                    {
                        var kustoModel = new ICMConfigKustoQueryModel();
                        var domainPart = dataExplorerMatch.Groups["domain"].Value; 
                        var path = dataExplorerMatch.Groups["path"].Value;
                        var parameters = ExtractQueryParameters(uri.Query);

                        kustoModel.Cloud = GetCloudEnvironmentFromDomain(domainPart).ToString();

                        var pathMatch = ClusterPathPattern.Match(path);
                        if (pathMatch.Success)
                        {
                            var extractedCluster = pathMatch.Groups[1].Value;
                            var clusterUriMatch = ClusterUriPattern.Match(extractedCluster);
                            if (clusterUriMatch.Success)
                            {
                                kustoModel.Cluster = extractedCluster;
                                kustoModel.Cloud = GetCloudEnvironmentFromDomain(clusterUriMatch.Groups["domain"].Value).ToString();
                            }
                            else if (!string.IsNullOrEmpty(extractedCluster))
                            {
                                kustoModel.Cluster = $"{extractedCluster}.kusto.{GetKustoDomainFromDataExplorerDomain(domainPart)}";
                            }

                            if (pathMatch.Groups[2].Success)
                            {
                                kustoModel.Database = pathMatch.Groups[2].Value;
                            }
                        }

                        if (string.IsNullOrEmpty(kustoModel.Cluster) && parameters.TryGetValue("cluster", out var clusterFromParams))
                        {
                            var clusterUriMatch = ClusterUriPattern.Match(clusterFromParams);
                            if (clusterUriMatch.Success)
                            {
                                kustoModel.Cluster = clusterFromParams;
                                kustoModel.Cloud = GetCloudEnvironmentFromDomain(clusterUriMatch.Groups["domain"].Value).ToString();
                            }
                            else if (!string.IsNullOrEmpty(clusterFromParams))
                            {
                                kustoModel.Cluster = $"{clusterFromParams}.kusto.{GetKustoDomainFromDataExplorerDomain(domainPart)}";
                            }
                        }

                        if (string.IsNullOrEmpty(kustoModel.Database) && (parameters.TryGetValue("db", out var dbNameFromParams) || parameters.TryGetValue("database", out dbNameFromParams)))
                        {
                            kustoModel.Database = dbNameFromParams;
                        }
                        
                        foreach (var paramName in QueryParams)
                        {
                            if (parameters.TryGetValue(paramName, out var queryValue) && !string.IsNullOrEmpty(queryValue))
                            {
                                kustoModel.KustoQuery = DecodeKustoQuery(queryValue, logger) ?? string.Empty;
                                break;
                            }
                        }
                        return kustoModel;
                    }

                    logger?.LogDebug($"URL '{url}' does not match any known Kusto URL patterns");
                    return null;
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, $"Error parsing Kusto URL: {url}");
                    return null;
                }
            }

            /// <summary>
            /// Decodes a Kusto query from Base64/GZip encoding
            /// </summary>
            public static string? DecodeKustoQuery(string encodedQuery, ILogger? logger = null)
            {
                if (string.IsNullOrWhiteSpace(encodedQuery))
                    return string.Empty;

                try
                {
                    // Replace spaces with '+' before Base64 decoding, as spaces might have been substituted for '+' during URL decoding.
                    string correctedBase64Query = encodedQuery.Replace(' ', '+');

                    // Step 1: Base64 decode
                    byte[] compressedBytes;
                    try
                    {
                        compressedBytes = Convert.FromBase64String(correctedBase64Query);
                        if (compressedBytes.Length == 0)
                            return string.Empty;
                    }
                    catch (FormatException)
                    {
                        // Not Base64, return the query as-is (probably plain text)
                        return encodedQuery;
                    }

                    // Step 2: GZip decompress
                    try
                    {
                        using var input = new System.IO.MemoryStream(compressedBytes);
                        using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
                        using var output = new System.IO.MemoryStream();

                        gzip.CopyTo(output);
                        var result = System.Text.Encoding.UTF8.GetString(output.ToArray());
                        return string.IsNullOrWhiteSpace(result) ? encodedQuery : result;
                    }
                    catch (Exception ex)
                    {
                        logger?.LogDebug(ex, "GZip decompression failed, returning original query");
                        return encodedQuery;
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error decoding Kusto query");
                    return encodedQuery;
                }
            }

            /// <summary>
            /// Extracts query parameters from a URL query string
            /// </summary>
            public static Dictionary<string, string> ExtractQueryParameters(string queryString)
            {
                var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (string.IsNullOrWhiteSpace(queryString))
                    return parameters;

                try
                {
                    if (queryString.StartsWith("?"))
                        queryString = queryString.Substring(1);

                    var pairs = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);

                    foreach (var pair in pairs)
                    {
                        try
                        {
                            var equalIndex = pair.IndexOf('=');
                            if (equalIndex > 0)
                            {
                                var key = System.Net.WebUtility.UrlDecode(pair.Substring(0, equalIndex));
                                var value = System.Net.WebUtility.UrlDecode(pair.Substring(equalIndex + 1));
                                if (key != null && value != null)
                                    parameters[key] = value;
                            }
                            else
                            {
                                var key = System.Net.WebUtility.UrlDecode(pair);
                                if (key != null)
                                    parameters[key] = string.Empty;
                            }
                        }
                        catch
                        {
                            // Skip invalid parameters
                            continue;
                        }
                    }
                }
                catch
                {
                    // Return empty dictionary if processing fails
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                return parameters;
            }

            /// <summary>
            /// Extracts structured information from Kusto-related URLs and formats it as a JSON string.
            /// </summary>
            /// <param name="url">The URL to parse.</param>
            /// <param name="logger">Optional logger for debugging.</param>
            /// <returns>A JSON string containing extracted Kusto details, or null if not a Kusto URL or parsing/serialization fails.</returns>
            public static string? GetKustoDetailsAsJson(string url, ILogger? logger = null)
            {
                var kustoModel = ExtractKustoInfo(url, logger);

                if (kustoModel == null)
                {
                    logger?.LogDebug($"URL '{url}' could not be parsed as a Kusto URL or is invalid.");
                    return null;
                }
                
                try
                {
                    // Directly serialize the ICMConfigKustoQueryModel.
                    // This will include Cloud, Cluster, Database, KustoQuery, and Title (from base, likely null).
                    return JsonSerializer.Serialize(kustoModel, new JsonSerializerOptions { WriteIndented = false });
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, $"Error serializing Kusto details to JSON for URL: {url}");
                    return null;
                }
            }
            
            private static string GetKustoDomainFromDataExplorerDomain(string dataExplorerDomain) => dataExplorerDomain switch
            {
                "azure.com" => "windows.net",
                "azure.us" => "usgovcloudapi.net",
                "azure.cn" => "chinacloudapi.cn",
                "microsoftazure.de" => "cloudapi.de",
                _ => "windows.net" // Default to public
            };

            /// <summary>
            /// Maps domain to cloud environment
            /// </summary>
            private static AzureCloudEnvironment GetCloudEnvironmentFromDomain(string domain)
            {
                return domain.ToLowerInvariant() switch
                {
                    "windows.net" => AzureCloudEnvironment.Public,
                    "usgovcloudapi.net" => AzureCloudEnvironment.USGovernment,
                    "chinacloudapi.cn" => AzureCloudEnvironment.China,
                    "cloudapi.de" => AzureCloudEnvironment.Germany,
                    // Data Explorer domains
                    "azure.com" => AzureCloudEnvironment.Public,
                    "azure.us" => AzureCloudEnvironment.USGovernment,
                    "azure.cn" => AzureCloudEnvironment.China,
                    "microsoftazure.de" => AzureCloudEnvironment.Germany,
                    _ => AzureCloudEnvironment.Public // Default
                };
            }
        }

        /// <summary>
        /// Azure cloud environments for Kusto clusters
        /// </summary>
        public enum AzureCloudEnvironment
        {
            Public,
            USGovernment,
            China,
            Germany
        }
    }
}
