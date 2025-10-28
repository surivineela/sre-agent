using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Plugins.Implementation.CdbSDKDiagnosePlugin
{
    public sealed class ComponentRCA
    {
        // Routing team owning the component analysis result
        public string? RoutingTeam { get; set; }
        // Description and extra contextual details that explain the why of the recommendation
        public string? Description { get; set; }
        // Direct steps/recommendation to take for the internal oncall
        public string? Recommendation { get; set; }
        // Explanation for external consumers
        public string? PublicRecommendation { get; set; }
        // Related public documentation links
        public List<string>? RelatedDocumentationLinks { get; set; }
        // Boolean indicating if there were any issues found or not
        public bool FoundAnyIssues { get; set; }

        public static ComponentRCA NoIssue() => new ComponentRCA
        {
            RoutingTeam = null,
            Description = null,
            PublicRecommendation = null,
            Recommendation = null,
            RelatedDocumentationLinks = null,
            FoundAnyIssues = false
        };

        public ComponentRCA() { }

        public ComponentRCA(
            string? routingTeam,
            string? description,
            string? recommendation,
            string? publicRecommendation,
            List<string>? relatedDocumentationLinks,
            bool foundIssues)
        {
            RoutingTeam = routingTeam;
            Description = description;
            Recommendation = recommendation;
            PublicRecommendation = publicRecommendation;
            RelatedDocumentationLinks = relatedDocumentationLinks;
            FoundAnyIssues = foundIssues;
        }
    }

    public static class HelperFunctions
    {
        /// <summary>
        /// Walks a JObject recursively and invokes the handler when a node with the given name is encountered.
        /// Mirrors the Python walkJsonUntil(diagnosticsJson, nodeName, handler).
        /// </summary>
        public static void WalkJsonUntil(JToken diagnosticsJson, string nodeName, Action<JToken> handler)
        {
            // Case 1: current node has "name" property equal to nodeName
            if (diagnosticsJson.Type == JTokenType.Object &&
                diagnosticsJson["name"]?.ToString() == nodeName)
            {
                handler(diagnosticsJson);
            }

            // Case 2: property directly matches nodeName
            if (diagnosticsJson.Type == JTokenType.Object)
            {
                foreach (var property in ((JObject)diagnosticsJson).Properties())
                {
                    if (property.Name == nodeName)
                    {
                        handler(property.Value);
                    }
                }
            }

            // Case 3: recurse into "children"
            var children = diagnosticsJson["children"];
            if (children != null && children.Type == JTokenType.Array)
            {
                foreach (var child in children)
                {
                    WalkJsonUntil(child, nodeName, handler);
                }
            }

            // Case 4: recurse into "data"
            var data = diagnosticsJson["data"];
            if (data != null)
            {
                if (data.Type == JTokenType.Object)
                {
                    foreach (var property in ((JObject)data).Properties())
                    {
                        if (property.Name == nodeName)
                        {
                            handler(property.Value);
                        }
                        else if (property.Value.Type == JTokenType.Object)
                        {
                            WalkJsonUntil(property.Value, nodeName, handler);
                        }
                    }
                }
            }
        }

        // --- collect status/substatus from requests and detect connectivity timeouts ---
        public static Dictionary<string, List<string>> BuildStatusFromRequests(
            List<HTTPRequest> httpRequests, List<TCPRequest> tcpRequests)
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            void Add(string? status, string? sub)
            {
                if (string.IsNullOrWhiteSpace(status)) return;
                if (!map.TryGetValue(status, out var list))
                {
                    list = new List<string>();
                    map[status] = list;
                }
                if (!string.IsNullOrWhiteSpace(sub) && !list.Contains(sub!, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(sub!);
                }
            }

            foreach (var h in httpRequests)
                Add(h.StatusCode, h.SubStatusCode);

            foreach (var t in tcpRequests)
                Add(t.Status, t.Substatus);

            return map;
        }

    }

    public static class Util
    {
        // Equivalent of parseActivityId(contentString)
        public static string? ParseActivityId(string contentString, int startIndex = 0)
        {
            var initialPosition = contentString.IndexOf("ActivityId: ", startIndex, StringComparison.Ordinal);
            int finalPosition;

            if (initialPosition == -1)
            {
                // In case Gateway returns the internal diagnostics
                initialPosition = contentString.IndexOf("ActivityId\":\"", startIndex, StringComparison.Ordinal);
                if (initialPosition == -1)
                {
                    return null;
                }

                initialPosition += 13;
                finalPosition = contentString.IndexOf('"', initialPosition);
            }
            else
            {
                initialPosition += 12;
                finalPosition = contentString.IndexOf(';', initialPosition);
                var commaFinalPosition = contentString.IndexOf(',', initialPosition);

                if (finalPosition == -1 || commaFinalPosition > 0 && commaFinalPosition < finalPosition)
                {
                    finalPosition = commaFinalPosition;
                }
            }

            if (finalPosition == -1 || finalPosition <= initialPosition)
            {
                return null;
            }

            return contentString.Substring(initialPosition, finalPosition - initialPosition);
        }
    }
    public class ThreadInfo
    {
        public bool HasStarvation { get; set; }
        public int MinThreads { get; set; }
        public int MaxThreads { get; set; }
        public int AvailableThreads { get; set; }
        public int ThreadWaitIntervalInMs { get; set; }

        public ThreadInfo(
            bool hasStarvation,
            int minThreads,
            int maxThreads,
            int availableThreads,
            int threadWaitIntervalInMs)
        {
            HasStarvation = hasStarvation;
            MinThreads = minThreads;
            MaxThreads = maxThreads;
            AvailableThreads = availableThreads;
            ThreadWaitIntervalInMs = threadWaitIntervalInMs;
        }
    }
    public class CPUHistory
    {
        public double Value { get; set; }
        public double Memory { get; set; }
        public DateTime Time { get; set; }
        public ThreadInfo? ThreadInfo { get; set; }

        public CPUHistory(
            double value,
            double memory,
            DateTime time,
            ThreadInfo? threadInfo)
        {
            Value = value;
            Memory = memory;
            Time = time;
            ThreadInfo = threadInfo;
        }
    }
    public class TCPRequest
    {
        public DateTime ResponseTime { get; set; }
        public string Endpoint { get; set; }
        public string Tenant { get; set; }
        public int Port { get; set; }
        public string Partition { get; set; }
        public string Replica { get; set; }
        public string Status { get; set; }
        public string Substatus { get; set; }
        public double BackendLatency { get; set; }
        public bool IsTimeout { get; set; }
        public List<object> Timeline { get; set; } = new();
        public string OperationType { get; set; }
        public string ActivityId { get; set; }

        public TCPRequest(
            DateTime time,
            string endpoint,
            int port,
            string partition,
            string replica,
            string status,
            string substatus,
            object belatency,
            bool istimeout,
            string operationType,
            string activityId)
        {
            ResponseTime = time;
            Endpoint = endpoint;
            Tenant = endpoint.Split('.')[0];
            Port = port;
            Partition = partition;
            Replica = replica;
            Status = status;
            Substatus = substatus;

            // Handle both string and numeric latency inputs
            if (belatency is string latencyStr && double.TryParse(latencyStr, out var parsed))
            {
                BackendLatency = parsed;
            }
            else if (belatency is IConvertible convertible)
            {
                BackendLatency = convertible.ToDouble(CultureInfo.InvariantCulture);
            }
            else
            {
                BackendLatency = 0;
            }

            IsTimeout = istimeout;
            OperationType = operationType;
            ActivityId = activityId;
        }

        public string GetKey()
        {
            return $"{Endpoint}:{Port}/{Partition}/{Replica}";
        }

        public void SetTcpTimeline(List<object> timeline)
        {
            Timeline = timeline;
        }
    }
    public class HTTPRequest
    {
        public DateTime StartTime { get; set; }
        public double Duration { get; set; }
        public string Url { get; set; }
        public string ResourceType { get; set; }
        public string Operation { get; set; }
        public string StatusCode { get; set; }
        public string SubStatusCode { get; set; }
        public string ActivityId { get; set; }
        public bool IsTimeout { get; set; }

        public HTTPRequest(
            DateTime time,
            double duration,
            string url,
            string resourcetype,
            string operation,
            string statusCode,
            string subStatusCode,
            string activityId,
            bool istimeout)
        {
            StartTime = time;
            Duration = duration;
            Url = url;
            ResourceType = resourcetype;
            Operation = operation;
            StatusCode = statusCode;
            SubStatusCode = subStatusCode;
            ActivityId = activityId;
            IsTimeout = istimeout;
        }
    }
    public class ClientGatewayConfiguration
    {
        public string ConnectionLimit { get; set; }
        public string RequestTimeout { get; set; }
        public bool? UsesProxy { get; set; }
        public bool? UsesCustomHttpClient { get; set; }

        public ClientGatewayConfiguration(
            string connectionLimit,
            string requestTimeout,
            bool? usesProxy,
            bool? usesCustomHttpClient)
        {
            ConnectionLimit = connectionLimit;
            RequestTimeout = requestTimeout;
            UsesProxy = usesProxy;
            UsesCustomHttpClient = usesCustomHttpClient;
        }

        public string GetSummary()
        {
            var summaryMarkdown = new StringBuilder();
            summaryMarkdown.AppendLine($" * HTTP Max connection limit: {ConnectionLimit}");
            summaryMarkdown.AppendLine($" * Request Timeout: {RequestTimeout}");

            if (UsesProxy.HasValue && UsesProxy.Value)
            {
                summaryMarkdown.AppendLine($" * Using HTTP proxy: {UsesProxy}");
            }

            if (UsesCustomHttpClient.HasValue && UsesCustomHttpClient.Value)
            {
                summaryMarkdown.AppendLine($" * Using HTTPClientFactory/Custom HttpClient: {UsesCustomHttpClient}");
            }

            return summaryMarkdown.ToString();
        }
    }
    public class ClientTCPConfiguration
    {
        public string RequestTimeout { get; set; }
        public string IdleConnectionTimeout { get; set; }
        public string MaxRequestsPerChannel { get; set; }
        public string MaxConnectionsPerEndpoint { get; set; }
        public string PortReuseMode { get; set; }
        public bool? EndpointRediscoveryEnabled { get; set; }

        public ClientTCPConfiguration(
            string requestTimeout,
            string idleConnectionTimeout,
            string maxRequestsPerChannel,
            string maxConnectionsPerEndpoint,
            string portReuseMode,
            bool? endpointRediscoveryEnabled)
        {
            RequestTimeout = requestTimeout;
            IdleConnectionTimeout = idleConnectionTimeout;
            MaxRequestsPerChannel = maxRequestsPerChannel;
            MaxConnectionsPerEndpoint = maxConnectionsPerEndpoint;
            PortReuseMode = portReuseMode;
            EndpointRediscoveryEnabled = endpointRediscoveryEnabled;
        }

        public string GetSummary()
        {
            var summaryMarkdown = new StringBuilder();
            summaryMarkdown.AppendLine($" * TCP Connection timeout: {RequestTimeout}");

            if (!string.IsNullOrEmpty(IdleConnectionTimeout) && IdleConnectionTimeout != "-1")
            {
                summaryMarkdown.AppendLine($" * TCP Idle connection timeout: {IdleConnectionTimeout}");
            }

            if (!string.IsNullOrEmpty(MaxRequestsPerChannel))
            {
                summaryMarkdown.AppendLine($" * TCP Max requests per channel: {MaxRequestsPerChannel}");
            }

            if (!string.IsNullOrEmpty(MaxConnectionsPerEndpoint) && MaxConnectionsPerEndpoint != "65535")
            {
                summaryMarkdown.AppendLine($" * TCP Max connections per endpoint: {MaxConnectionsPerEndpoint}");
            }

            if (!string.IsNullOrEmpty(PortReuseMode))
            {
                summaryMarkdown.AppendLine($" * TCP Port reuse mode: {PortReuseMode}");
            }

            if (EndpointRediscoveryEnabled.HasValue && !EndpointRediscoveryEnabled.Value)
            {
                summaryMarkdown.AppendLine($" * TCP Endpoint rediscovery (detect server connection closing): {EndpointRediscoveryEnabled}");
            }

            return summaryMarkdown.ToString();
        }
    }
    public class ClientConfiguration
    {
        public string UserAgent { get; set; }
        public string Version { get; set; }
        public string? EnvironmentDescription { get; set; }
        public string? PlatformDescription { get; set; }
        public int? NumberOfClients { get; set; }
        public bool? CrossRegionalRequestsEnabled { get; set; }
        public string? ConsistencyOverride { get; set; }
        public bool? UsePreferredRegions { get; set; }
        public bool? BulkMode { get; set; }
        public DateTime? CreatedTime { get; set; }
        public ClientGatewayConfiguration? GatewayConfig { get; set; }
        public ClientTCPConfiguration? TCPConfig { get; set; }
        public string? RegionalConfiguration { get; set; }
        public int? ProcessorCount { get; set; }

        public ClientConfiguration(
            string userAgent,
            string version,
            string? environmentDescription,
            string? platformDescription,
            int? numberOfClients,
            bool? crossRegionalRequestsEnabled,
            string? consistencyOverride,
            bool? usePreferredRegions,
            bool? bulkMode,
            DateTime? createdTime,
            ClientGatewayConfiguration? gwConfig,
            ClientTCPConfiguration? tcpConfig,
            string? regionalConfiguration,
            int? processorCount)
        {
            UserAgent = userAgent;
            Version = version;
            EnvironmentDescription = environmentDescription;
            PlatformDescription = platformDescription;
            NumberOfClients = numberOfClients;
            CrossRegionalRequestsEnabled = crossRegionalRequestsEnabled;
            ConsistencyOverride = consistencyOverride;
            UsePreferredRegions = usePreferredRegions;
            BulkMode = bulkMode;
            CreatedTime = createdTime;
            GatewayConfig = gwConfig;
            TCPConfig = tcpConfig;
            RegionalConfiguration = regionalConfiguration;
            ProcessorCount = processorCount;
        }

        public string GetSummary()
        {
            var summaryMarkdown = new StringBuilder();
            summaryMarkdown.AppendLine($"\nClient version **{Version}**");

            if (CreatedTime.HasValue)
            {
                summaryMarkdown.Append($" was created at {CreatedTime.Value:MM/dd/yyyy HH:mm:ss} UTC");
            }

            if (!string.IsNullOrEmpty(EnvironmentDescription))
            {
                summaryMarkdown.Append($", running on {EnvironmentDescription}");
            }

            if (!string.IsNullOrEmpty(PlatformDescription))
            {
                summaryMarkdown.Append($" and built as {PlatformDescription}");
            }

            if (UsePreferredRegions.HasValue)
            {
                if (UsePreferredRegions.Value && !string.IsNullOrEmpty(RegionalConfiguration))
                {
                    summaryMarkdown.Append($" with PreferredRegions set {RegionalConfiguration}");
                }
                else
                {
                    summaryMarkdown.Append(" with PreferredRegions **not** set");
                }
            }

            summaryMarkdown.Append($". User agent = {UserAgent}");
            summaryMarkdown.AppendLine("\n### Client configuration");

            if (!string.IsNullOrEmpty(ConsistencyOverride) &&
                !string.Equals(ConsistencyOverride, "notset", StringComparison.OrdinalIgnoreCase))
            {
                summaryMarkdown.AppendLine($" * Consistency override: {ConsistencyOverride}");
            }

            if (CrossRegionalRequestsEnabled.HasValue && !CrossRegionalRequestsEnabled.Value)
            {
                summaryMarkdown.AppendLine(" * Cross regional failover configuration (LimitToEndpoint/EndpointRediscovery): Disabled");
            }

            if (BulkMode.HasValue && BulkMode.Value)
            {
                summaryMarkdown.AppendLine(" * Bulk mode: Enabled");
            }

            if (GatewayConfig != null)
            {
                summaryMarkdown.Append(GatewayConfig.GetSummary());
            }

            if (TCPConfig != null)
            {
                summaryMarkdown.Append(TCPConfig.GetSummary());
            }

            if (ProcessorCount.HasValue && ProcessorCount.Value != 0)
            {
                summaryMarkdown.AppendLine($" * Processor Count: {ProcessorCount}");
            }

            return summaryMarkdown.ToString();
        }
    }
    public class Diagnostics
    {
        public bool TraceKustoQueries { get; set; }
        public bool HasCrossRegionRequests { get; set; }
        public ClientConfiguration? ClientConfig { get; set; }
        public List<CPUHistory> CPUHistory { get; set; }
        public string Operation { get; set; }
        public int TotalLatencyMs { get; set; }
        public List<TCPRequest> TCPRequests { get; set; }
        public List<HTTPRequest> HTTPRequests { get; set; }
        public Dictionary<string, List<string>> StatusSubstatus { get; set; } = new();
        public bool HasConnectivityIssues { get; set; }
        public bool HasBackend410s { get; set; }
        public bool HasBackendMetadataThrottling { get; set; }
        public bool HasHighCPU { get; set; }
        public string HasHighCPU_rca { get; set; } = "";
        public string? Processors_rca { get; set; }
        public bool HasThreadStarvation { get; set; }
        public string HasThreadStarvation_rca { get; set; } = "";
        public ComponentRCA? Gateway_rca { get; set; }
        public ComponentRCA? Backend_rca { get; set; }
        public (string, List<string>)? HasStatusSubstatus_rca { get; set; }
        public string HasTCPHighLatency_rca { get; set; } = "";
        public bool HasTCPHighTransitTime { get; set; }
        public string HasHighBackendLatency_rca { get; set; } = "";
        public string HasHighBackendLatency_isQuery { get; set; } = "";

        public Diagnostics(
            bool traceKustoQueries,
            string operation,
            int totalLatencyMs,
            int? contactedRegions,
            ClientConfiguration clientConfig,
            List<CPUHistory> cpuHistoryArray,
            List<TCPRequest> tcpRequests,
            List<HTTPRequest> httpRequests)
        {
            TraceKustoQueries = traceKustoQueries;
            Operation = operation;
            TotalLatencyMs = totalLatencyMs;
            HasCrossRegionRequests = contactedRegions.HasValue && contactedRegions > 1;
            ClientConfig = clientConfig;
            CPUHistory = cpuHistoryArray;
            TCPRequests = tcpRequests;
            HTTPRequests = httpRequests;
        }

        private string GetSummary()
        {
            var summaryMarkdown = new StringBuilder("# Summary\n");

            if (!string.IsNullOrEmpty(Operation))
            {
                summaryMarkdown.AppendLine(
                    $"These diagnostics are for a **{Operation}** operation, with a duration of **{TotalLatencyMs} milliseconds**.");
            }

            if (ClientConfig != null)
            {
                summaryMarkdown.Append(ClientConfig.GetSummary());
            }

            if (StatusSubstatus.Count > 0)
            {
                var statusSubstatusSummary = new StringBuilder();

                foreach (var kvp in StatusSubstatus)
                {
                    if (string.IsNullOrEmpty(kvp.Key)) continue;
                    foreach (var substatus in kvp.Value)
                    {
                        statusSubstatusSummary.AppendLine($"{kvp.Key} | {substatus}");
                    }
                }

                if (statusSubstatusSummary.Length > 0)
                {
                    summaryMarkdown.AppendLine("\n### Summary of status / substatus codes");
                    summaryMarkdown.AppendLine("Status code | sub status code");
                    summaryMarkdown.AppendLine("--- | ---");
                    summaryMarkdown.Append(statusSubstatusSummary);
                }
            }

            return summaryMarkdown.ToString();
        }

        private void AnalyzeCpuInfo()
        {
            foreach (var cpu in CPUHistory)
            {
                if (cpu.Value > 70 && !HasHighCPU)
                {
                    HasHighCPU = true;
                    HasHighCPU_rca = $"Logs indicate high CPU at {cpu.Time}. CPU {cpu.Value}%.";
                }

                if (cpu.ThreadInfo != null && cpu.ThreadInfo.HasStarvation && !HasThreadStarvation)
                {
                    HasThreadStarvation = true;
                    HasThreadStarvation_rca =
                        $"Logs indicate thread starvation at {cpu.Time}. ThreadWaitIntervalInMs (wait time until an async Task is executed) {cpu.ThreadInfo.ThreadWaitIntervalInMs}, " +
                        $"MaxThreads {cpu.ThreadInfo.MaxThreads}, MinThreads {cpu.ThreadInfo.MinThreads}, AvailableThreads {cpu.ThreadInfo.AvailableThreads}.";
                }
            }

            if (ClientConfig?.ProcessorCount == 1)
            {
                Processors_rca =
                    "Low available processor count detected. Having 1 available processor in the compute environment will affect connectivity causing timeouts and service unavailable errors.";
            }
        }

        private void AnalyzeResponses()
        {
            var statusAnalysis = "";
            var links = new List<string>();

            if (Backend_rca == null && Gateway_rca == null)
            {
                // Throttling cases
                if (StatusSubstatus.ContainsKey("429") || StatusSubstatus.ContainsKey("TooManyRequests"))
                {
                    if (StatusSubstatus.ContainsKey("429") && StatusSubstatus["429"].Contains("3200") ||
                        StatusSubstatus.ContainsKey("TooManyRequests") &&
                         StatusSubstatus["TooManyRequests"].Contains("RUBudgetExceeded"))
                    {
                        statusAnalysis =
                            "Detected throttling due to exceeding provisioned throughput, refer to the provided link for more information.";
                        links.Add("https://docs.microsoft.com/azure/cosmos-db/sql/troubleshoot-request-rate-too-large");
                    }
                    else if (StatusSubstatus.ContainsKey("429") && StatusSubstatus["429"].Contains("10003"))
                    {
                        statusAnalysis =
                            "Detected throttling due to Global Throughput Control, related to customer configuration.";
                        links.Add("https://learn.microsoft.com/azure/cosmos-db/nosql/throughput-control-spark");
                    }
                    else
                    {
                        var substatus = StatusSubstatus.ContainsKey("429")
                            ? StatusSubstatus["429"].FirstOrDefault()
                            : StatusSubstatus["TooManyRequests"].FirstOrDefault();

                        statusAnalysis =
                            $"Detected 429s with sub status code {substatus}, refer to internal routing table documentation.";
                        HasBackendMetadataThrottling = true;
                    }
                }

                // 404/1013 case
                if (StatusSubstatus.ContainsKey("404") && StatusSubstatus["404"].Contains("1013") ||
                    StatusSubstatus.ContainsKey("NotFound") &&
                     StatusSubstatus["NotFound"].Contains("CollectionCreateInProgress"))
                {
                    statusAnalysis =
                        "Detected 404/1013 — the collection was not yet ready to receive requests after creation.";
                }

                // 412 case
                if (StatusSubstatus.ContainsKey("412") || StatusSubstatus.ContainsKey("PreconditionFailed"))
                {
                    statusAnalysis =
                        "Detected 412 (PreconditionFailed) — Optimistic Concurrency. The request should be retried after reading the latest version of the resource.";
                    links.Add("https://aka.ms/CosmosDB/sql/errors/precondition-failed");
                }

                // 403
                if ((StatusSubstatus.ContainsKey("403") || StatusSubstatus.ContainsKey("Forbidden")) &&
                    !HasCrossRegionRequests)
                {
                    statusAnalysis = "Detected 403 (Forbidden).";
                    links.Add("https://aka.ms/cosmosdb-tsg-forbidden");
                }

                // 401
                if (StatusSubstatus.ContainsKey("401") || StatusSubstatus.ContainsKey("Unauthorized"))
                {
                    statusAnalysis = "Detected 401 (Unauthorized).";
                    links.Add("https://aka.ms/cosmosdb-tsg-unauthorized");
                }

                // 503 and variants
                if (StatusSubstatus.ContainsKey("503") || StatusSubstatus.ContainsKey("ServiceUnavailable"))
                {
                    var sub = StatusSubstatus.ContainsKey("503")
                        ? StatusSubstatus["503"].FirstOrDefault()
                        : StatusSubstatus["ServiceUnavailable"].FirstOrDefault();

                    if (sub is "21007" or "Server_ReadQuorumNotMet")
                    {
                        statusAnalysis =
                            "Detected 503 (Service Unavailable) due to quorum not met. Retryable.";
                        links.Add("https://docs.microsoft.com/azure/cosmos-db/sql/conceptual-resilient-sdk-applications#timeouts-and-connectivity-related-failures-http-408503");
                    }
                    else if (sub is "21006" or "GlobalStrongWriteBarrierNotMet")
                    {
                        statusAnalysis =
                            "Detected 503 (Service Unavailable) due to barrier retries. Retryable.";
                        links.Add("https://docs.microsoft.com/azure/cosmos-db/sql/conceptual-resilient-sdk-applications#timeouts-and-connectivity-related-failures-http-408503");
                    }
                    else
                    {
                        statusAnalysis =
                            "Detected 503 (Service Unavailable) from the service — transient connectivity issue.";
                        links.Add("https://docs.microsoft.com/azure/cosmos-db/sql/conceptual-resilient-sdk-applications#timeouts-and-connectivity-related-failures-http-408503");
                    }
                }

                // 410
                if ((StatusSubstatus.ContainsKey("410") || StatusSubstatus.ContainsKey("Gone")) &&
                    !HasConnectivityIssues)
                {
                    HasBackend410s = true;
                    statusAnalysis +=
                        "Detected 410 (Gone) from the server that would cause retries and high latency.";
                }

                // Java SDK 0/10001-10002 client-side cases
                if (StatusSubstatus.TryGetValue("0", out var zeroCodes) &&
                    (zeroCodes.Contains("10001") || zeroCodes.Contains("10002")))
                {
                    statusAnalysis =
                        "Detected HTTP timeouts trying to reach Gateway (0/10001 or 10002). Retryable.";
                    links.Add("https://docs.microsoft.com/azure/cosmos-db/sql/conceptual-resilient-sdk-applications#timeouts-and-connectivity-related-failures-http-408503");
                }
            }

            if (!string.IsNullOrEmpty(statusAnalysis))
            {
                HasStatusSubstatus_rca = (statusAnalysis, links);
            }
        }

        public ComponentRCA GenerateFinalRCA()
        {
            AnalyzeResponses();
            GetSummary();
            AnalyzeCpuInfo();

            if (Gateway_rca != null) return Gateway_rca;
            if (Backend_rca != null) return Backend_rca;

            var rca = string.Empty;
            var publicRca = string.Empty;
            var recommendedAction = string.Empty;
            var routingTeam = string.Empty;
            var links = new List<string>();

            // --- Connectivity Issues ---
            if (HasConnectivityIssues)
            {
                if (HasHighCPU)
                {
                    rca += HasHighCPU_rca + "<br><br>";
                    publicRca += HasHighCPU_rca + "<br><br>";
                }

                if (HasThreadStarvation)
                {
                    rca += HasThreadStarvation_rca + "<br><br>";
                    publicRca += HasThreadStarvation_rca + "<br><br>";
                }

                if (!HasHighCPU && !HasThreadStarvation)
                {
                    // No apparent reason but there were timeouts
                    rca = "Timeouts were detected but could not determine known client side reasons. " +
                          "They could be related to network conditions. Timeouts are retryable errors that can sometimes cause high end-to-end latency " +
                          "but should be transient and user application should have retry mechanism in place. ";
                    publicRca = rca;
                    links.Add("https://docs.microsoft.com/azure/cosmos-db/sql/conceptual-resilient-sdk-applications#timeouts-and-connectivity-related-failures-http-408503");
                }
            }
            else
            {
                if (HasStatusSubstatus_rca != null)
                {
                    rca += HasStatusSubstatus_rca.Value.Item1 + "<br><br>";
                    publicRca += HasStatusSubstatus_rca.Value.Item1 + "<br><br>";
                    links = HasStatusSubstatus_rca.Value.Item2;
                }

                if (HasHighCPU)
                {
                    rca += HasHighCPU_rca + "<br><br>";
                    publicRca += HasHighCPU_rca + "<br><br>";
                }

                if (HasThreadStarvation)
                {
                    rca += HasThreadStarvation_rca + "<br><br>";
                    publicRca += HasThreadStarvation_rca + "<br><br>";
                }
            }

            // --- Backend latency or TCP latency ---
            if (!string.IsNullOrEmpty(HasHighBackendLatency_rca))
            {
                if (!string.IsNullOrEmpty(HasHighBackendLatency_isQuery))
                    rca = HasHighBackendLatency_isQuery;

                rca += HasHighBackendLatency_rca + "<br><br>";
            }
            else if (!string.IsNullOrEmpty(HasTCPHighLatency_rca))
            {
                // High latency on TCP events/wire
                // (Python displayed HTML; in C#, we skip UI rendering)

                if (HasTCPHighTransitTime)
                {
                    rca += "Diagnostics show high Transit time which mean that the time is mostly spent on the network interaction.<br><br>";
                    publicRca += "Diagnostics show high Transit time which mean that the time is mostly spent on the network interaction (the request is sent and response is taking time). <br><br>";

                    if (HasCrossRegionRequests)
                    {
                        rca += "Operation includes cross-region requests which would surface as higher latency.<br><br>";
                        publicRca += "Operation includes cross-region requests which would surface as higher latency. <br><br>";
                        links.Add("https://docs.microsoft.com/azure/cosmos-db/sql/troubleshoot-sdk-availability");
                    }

                    if (ClientConfig != null &&
                        ClientConfig.UsePreferredRegions.HasValue &&
                        !ClientConfig.UsePreferredRegions.Value)
                    {
                        rca += "Client configuration does not include preferred regions, make sure these are correctly configured to target the closest region.<br><br>";
                        publicRca += "Client configuration does not include preferred regions, for best latency, make sure the configuration includes regional affinity.<br><br>";
                    }
                }
                else
                {
                    rca += "Diagnostics show high time spent on the TCP channels, see the Notebook details for the particular events and causes.<br><br>";
                    publicRca += "Diagnostics show high time spent on the TCP channels during request processing.<br><br>";
                    links.Add("https://learn.microsoft.com/azure/cosmos-db/nosql/troubleshoot-dotnet-sdk-slow-request?tabs=cpu-new#requesttimeline");
                }
            }

            // --- Client configuration checks ---
            if (ClientConfig != null && ClientConfig.NumberOfClients.HasValue && ClientConfig.NumberOfClients.Value > 1)
            {
                rca += $"There are {ClientConfig.NumberOfClients} client instances detected, this could indicate that the customer is not following the singleton pattern and is expected if the customer is connecting to multiple accounts. ";
                publicRca += $"There are {ClientConfig.NumberOfClients} client instances detected, this could indicate that the singleton pattern is not being followed. It is only expected if your application is connecting to multiple accounts. ";
                links.Add("https://learn.microsoft.com/azure/cosmos-db/sql/conceptual-resilient-sdk-applications#client-instances-and-connections");
            }

            if (ClientConfig != null &&
                ClientConfig.CrossRegionalRequestsEnabled.HasValue &&
                !ClientConfig.CrossRegionalRequestsEnabled.Value)
            {
                rca += "Client configuration indicates cross-regional failover is disabled, which is not advised unless explicitly intended.<br><br>";
                publicRca += "Client configuration indicates cross-regional failover is disabled, which is not advised unless explicitly intended.<br><br>";
                links.Add("https://docs.microsoft.com/azure/cosmos-db/sql/troubleshoot-sdk-availability");
            }

            if (!string.IsNullOrEmpty(Processors_rca))
            {
                rca += Processors_rca + "<br><br>";
                publicRca += Processors_rca + "<br><br>";
            }

            // --- Routing and Recommendations ---
            if (HasConnectivityIssues)
            {
                routingTeam = "Customer";
                recommendedAction = "Follow up with the customer with the highlights of this investigation to understand any potential client side issues.";
            }
            else if (!string.IsNullOrEmpty(HasHighBackendLatency_rca))
            {
                if (!string.IsNullOrEmpty(HasHighBackendLatency_isQuery))
                {
                    routingTeam = "Query Engine for SQL";
                    recommendedAction = "Diagnostics show that there is high backend query latency, it could mean that the query is expensive. Please engage the Query team and share the notebook analysis.";
                    publicRca = "Diagnostics show that there is high backend query latency, it could mean that the query is expensive, keep in mind that queries have no latency SLA. If this latency is unexpected please continue with the support request.";
                }
                else
                {
                    routingTeam = "Availability and Store";
                    recommendedAction = "Diagnostics show that there is high backend latency for the operations, please engage Availability and Store team and share the notebook analysis.";
                    publicRca = "Diagnostics show that there is high backend latency for the operations, if this is impacting P99/1h latency on your account please continue with the support request.";
                }
            }
            else if (HasBackendMetadataThrottling)
            {
                routingTeam = "Availability and Store";
                recommendedAction = "There is throttling HTTP 429 with Substatus != 3200, please follow up with Availability and Store if this is affecting customer availability";
                publicRca = "We have found throttling retries due to HTTP 429 responses from the service. If this is impacting P99/1h availability of your application please continue with the support request<br>";
            }
            else if (HasBackend410s)
            {
                routingTeam = "Availability and Store";
                recommendedAction = "Please **review the account SLA and Availability dashboard** and engage Availability and Store team and share the notebook analysis.";
                publicRca = "We have found transient retries due to HTTP 410 responses from the service. If this is impacting P99/1h availability of your application please continue with the support request<br>";
            }
            else if (!string.IsNullOrEmpty(rca))
            {
                routingTeam = "Customer";
                recommendedAction = "Follow up with the customer with the highlights of this investigation to understand any issues. The notebook view can contain more details.";
                publicRca = "We have found the following issues analyzing your logs: <br>" + publicRca;
            }

            return new ComponentRCA(
                routingTeam,
                rca,
                recommendedAction,
                publicRca,
                links,
                !string.IsNullOrEmpty(rca)
            );
        }
    }
    public class SDKDiagnosticsAnalysisResult
    {
        public string? Error { get; set; }
        public ComponentRCA? RCA { get; set; }

        public SDKDiagnosticsAnalysisResult(string? error, ComponentRCA? rca)
        {
            Error = error;
            RCA = rca;
        }
    }
    public class DiagnosticsAnalysis
    {
        public bool TraceKusto { get; set; }

        public DiagnosticsAnalysis(bool traceKusto)
        {
            TraceKusto = traceKusto;
        }

        public SDKDiagnosticsAnalysisResult Analyze(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return new SDKDiagnosticsAnalysisResult("error or diagnostics are empty", null);
            }

            SDKDiagnosticsAnalysisResult result;
            string language;

            if (error.Contains("java", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Detected Java diagnostics");
                language = "Java";
                var javaAnalysis = new JavaDiagnosticsAnalysis(TraceKusto);
                result = javaAnalysis.Analyze(error);
            }
            else if (error.Contains("python", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Detected Python diagnostics");
                language = "Python";
                var pythonAnalysis = new PythonDiagnosticsAnalysis(TraceKusto);
                result = pythonAnalysis.Analyze(error);
            }
            else if (error.Contains("nodejs", StringComparison.OrdinalIgnoreCase) ||
                        error.Contains("azure-cosmos-js", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Detected NodeJS diagnostics");
                language = "NodeJS";
                var nodeAnalysis = new NodeDiagnosticsAnalysis(TraceKusto);
                result = nodeAnalysis.Analyze(error);
            }
            else
            {
                Console.WriteLine("Detected .NET diagnostics");
                language = "NET";
                var netAnalysis = new NETDiagnosticsAnalysis(TraceKusto);
                result = netAnalysis.Analyze(error);
            }

            // If issues were found, prefix with language tag
            if (result.RCA != null && result.RCA.FoundAnyIssues)
            {
                result.RCA.Description = $"[{language}] {result.RCA.Description}";
                return result;
            }

            // If no RCA found but contains aka.ms link, surface that
            if (error.Contains("aka.ms", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(error, @"aka\.ms/([-|\w|/]*)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var akaLink = match.Groups[1].Value;
                    return new SDKDiagnosticsAnalysisResult(
                        null,
                        new ComponentRCA(
                            routingTeam: "Customer",
                            description: "The diagnostics contain a relevant link, please share it with the customer.",
                            recommendation: "Follow up with the customer and share the details.",
                            publicRecommendation: "Logs indicate that there is a documentation link related to the issue, please follow the provided link for more details.",
                            relatedDocumentationLinks: new List<string> { $"https://aka.ms/{akaLink}" },
                            foundIssues: true
                        )
                    );
                }
            }

            return result;
        }
    }
    public class PythonDiagnosticsAnalysis
    {
        public bool TraceKusto { get; set; }

        public PythonDiagnosticsAnalysis(bool traceKusto)
        {
            TraceKusto = traceKusto;
        }

        public SDKDiagnosticsAnalysisResult Analyze(string error)
        {
            var activityId = Util.ParseActivityId(error);
            if (string.IsNullOrEmpty(activityId))
            {
                return new SDKDiagnosticsAnalysisResult(
                    "Cannot find any activityId in the details",
                    null
                );
            }

            var httpRequests = new List<HTTPRequest>
            {
                new HTTPRequest
                (
                    time: DateTime.UtcNow.Date,      // equivalent to date.today()
                    duration: 0,
                    url: string.Empty,
                    resourcetype: string.Empty,
                    operation: string.Empty,
                    statusCode: string.Empty,
                    subStatusCode: string.Empty,
                    activityId: activityId,
                    istimeout: false
                )
            };

            var diagnostics = new Diagnostics(
                traceKustoQueries: TraceKusto,
                operation: string.Empty,
                totalLatencyMs: 0,
                contactedRegions: 0,
                clientConfig: new ClientConfiguration(
                    userAgent: string.Empty,
                    version: string.Empty,
                    environmentDescription: null,
                    platformDescription: null,
                    numberOfClients: null,
                    crossRegionalRequestsEnabled: null,
                    consistencyOverride: null,
                    usePreferredRegions: null,
                    bulkMode: null,
                    createdTime: null,
                    gwConfig: null,
                    tcpConfig: null,
                    regionalConfiguration: null,
                    processorCount: null
                ),
                cpuHistoryArray: new List<CPUHistory>(),
                tcpRequests: new List<TCPRequest>(),
                httpRequests: httpRequests
            );

            var rca = diagnostics.GenerateFinalRCA();

            return new SDKDiagnosticsAnalysisResult(null, rca);
        }
    }
    public class NodeDiagnosticsAnalysis
    {
        public bool TraceKusto { get; set; }

        public NodeDiagnosticsAnalysis(bool traceKusto)
        {
            TraceKusto = traceKusto;
        }

        public SDKDiagnosticsAnalysisResult Analyze(string error)
        {
            // Extract ActivityId from the diagnostics error text
            var activityId = Util.ParseActivityId(error);

            if (string.IsNullOrEmpty(activityId))
            {
                return new SDKDiagnosticsAnalysisResult(
                    "Cannot find any activityId in the details",
                    null
                );
            }

            // Create a single HTTPRequest (mirroring Python structure)
            var httpRequests = new List<HTTPRequest>
        {
            new HTTPRequest(
                time: DateTime.UtcNow.Date,      // equivalent to Python's date.today()
                duration: 0,
                url: string.Empty,
                resourcetype: string.Empty,
                operation: string.Empty,
                statusCode: string.Empty,
                subStatusCode: string.Empty,
                activityId: activityId,
                istimeout: false
            )
        };

            // Create Diagnostics object with empty/default values
            var diagnostics = new Diagnostics(
                traceKustoQueries: TraceKusto,
                operation: string.Empty,
                totalLatencyMs: 0,
                contactedRegions: 0,
                clientConfig: new ClientConfiguration(
                    userAgent: string.Empty,
                    version: string.Empty,
                    environmentDescription: null,
                    platformDescription: null,
                    numberOfClients: null,
                    crossRegionalRequestsEnabled: null,
                    consistencyOverride: null,
                    usePreferredRegions: null,
                    bulkMode: null,
                    createdTime: null,
                    gwConfig: null,
                    tcpConfig: null,
                    regionalConfiguration: null,
                    processorCount: null
                ),
                cpuHistoryArray: new List<CPUHistory>(),
                tcpRequests: new List<TCPRequest>(),
                httpRequests: httpRequests
            );

            // Generate RCA asynchronously
            var rca = diagnostics.GenerateFinalRCA();

            // Return analysis result
            return new SDKDiagnosticsAnalysisResult(null, rca);
        }
    }
    public class NETDiagnosticsAnalysis
    {
        public bool TraceKusto { get; set; }

        public NETDiagnosticsAnalysis(bool traceKusto)
        {
            TraceKusto = traceKusto;
        }

        public SDKDiagnosticsAnalysisResult Analyze(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return new SDKDiagnosticsAnalysisResult("error or diagnostics are empty", null);
            }

            // -------------- helper: find the end of diagnostics blob --------------
            static int FindDiagnosticsEnd(string text, int initialPosition)
            {
                var finalPosition = text.IndexOf("}]} ", initialPosition, StringComparison.Ordinal);
                if (finalPosition == -1)
                {
                    finalPosition = text.IndexOf("}]}\"", initialPosition, StringComparison.Ordinal);
                }
                if (finalPosition > -1)
                {
                    // include the trailing 3 chars in the slice (same as Python logic)
                    return finalPosition + 3;
                }
                else
                {
                    // fallback to last closing brace
                    finalPosition = text.LastIndexOf('}');
                    if (finalPosition > -1) return finalPosition + 1;
                }
                return -1;
            }

            // -------------- helper: parse the .NET diagnostics string into JObject --------------
            object ParseDiagnosticsString(string diagnosticString)
            {
                var parsedDiagnostics = diagnosticString;

                // Are diagnostics single-quoted? Normalize to JSON double quotes
                if (parsedDiagnostics.Contains("'User Agent':'cosmos-netstandard-sdk", StringComparison.Ordinal))
                {
                    parsedDiagnostics = parsedDiagnostics.Replace("'", "\"");
                }

                if (parsedDiagnostics.Contains("--- Cosmos Diagnostics ---", StringComparison.Ordinal) &&
                    !parsedDiagnostics.Contains("\"Summary\":{}", StringComparison.Ordinal))
                {
                    // Case 1: Exceptions where diagnostics are on one line starting after the marker
                    var start = parsedDiagnostics.IndexOf("--- Cosmos Diagnostics ---", StringComparison.Ordinal) +
                                "--- Cosmos Diagnostics ---".Length;
                    var end = FindDiagnosticsEnd(parsedDiagnostics, start);
                    if (end == -1) end = parsedDiagnostics.Length;
                    parsedDiagnostics = parsedDiagnostics.Substring(start, end - start);
                }
                else if (parsedDiagnostics.Contains("\"Summary\":", StringComparison.Ordinal) &&
                            !parsedDiagnostics.Contains("\"Summary\":{}", StringComparison.Ordinal))
                {
                    // Case 3 (new): Raw JSON with "Summary"
                    var start = parsedDiagnostics.IndexOf("\"Summary\":", StringComparison.Ordinal);
                    var end = FindDiagnosticsEnd(parsedDiagnostics, start);
                    if (end == -1) end = parsedDiagnostics.Length;
                    parsedDiagnostics = "{" + parsedDiagnostics.Substring(start, end - start);
                }
                else if (parsedDiagnostics.Contains("{\"name\":\"", StringComparison.Ordinal))
                {
                    // Case 3 (old): Raw JSON starting at {"name":
                    var start = parsedDiagnostics.IndexOf("{\"name\":\"", StringComparison.Ordinal);
                    var end = FindDiagnosticsEnd(parsedDiagnostics, start);
                    if (end == -1) end = parsedDiagnostics.Length;
                    parsedDiagnostics = parsedDiagnostics.Substring(start, end - start);
                }
                else if (parsedDiagnostics.Contains("ActivityId", StringComparison.Ordinal))
                {
                    // Case 4: Some random non-JSON string that at least has an ActivityId
                    var activityId = Util.ParseActivityId(parsedDiagnostics);
                    if (!string.IsNullOrEmpty(activityId))
                    {
                        // Synthesize a minimal JSON structure the downstream expects
                        var synthetic = new JObject
                        {
                            ["Client Side Request Stats"] = new JObject
                            {
                                ["HttpResponseStats"] = new JArray
                                {
                                    new JObject
                                    {
                                        ["ActivityId"]   = activityId,
                                        ["StartTimeUTC"] = "2021-10-07T21:45:35.8076598Z",
                                        ["DurationInMs"] = 0,
                                        ["RequestUri"]   = "",
                                        ["ResourceType"] = "",
                                        ["HttpMethod"]   = "",
                                        ["StatusCode"]   = "None"
                                    }
                                }
                            }
                        };
                        parsedDiagnostics = JsonConvert.SerializeObject(synthetic);
                    }
                }

                // Try to parse JSON; if it fails, try a second pass removing control breaks
                try
                {
                    var token = JsonConvert.DeserializeObject(parsedDiagnostics);
                    if (token is JObject or JArray)
                    {
                        return token!;
                    }
                    // if not an object-like, treat as parse error
                    throw new JsonReaderException("No valid input");
                }
                catch (JsonReaderException)
                {
                    try
                    {
                        // Relax: collapse newlines and unescape escaped sequences a bit
                        var compact = Regex.Replace(parsedDiagnostics, @"\r?\n", " ");
                        var token = JsonConvert.DeserializeObject(compact);
                        if (token is JObject or JArray) return token!;
                        throw new JsonReaderException("No valid input (second attempt)");
                    }
                    catch (JsonReaderException e2)
                    {
                        // Match Python behavior: return an SDKDiagnosticsAnalysisResult carrying an error
                        return new SDKDiagnosticsAnalysisResult(
                            $"Could not parse the exception details as a .NET SDK Diagnostics {e2.Message}", null);
                    }
                }
            }

            var parsedObj = ParseDiagnosticsString(error);
            if (parsedObj is SDKDiagnosticsAnalysisResult earlyFail)
            {
                return earlyFail; // parsing error path
            }

            var parsedJson = parsedObj as JToken ?? new JObject();

            // --------- Variables collected during parsing (mirroring Python) ----------
            string? operation = null;
            int? totalLatencyMs = null;
            int? contactedRegions = null;

            string? userAgent = null;
            string? version = null;
            string? environmentDescription = null;
            string? platformDescription = null;
            int? numberOfClients = null;
            bool? crossRegionalRequestsEnabled = null;
            string? regionalConfiguration = null;
            string? consistencyOverride = null;
            bool? userPreferredRegions = null;
            bool? usingBulk = null;
            DateTime? createdTime = null;
            ClientGatewayConfiguration? gwConfig = null;
            ClientTCPConfiguration? tcpConfig = null;
            int? processorCount = null;

            // ---------- helpers: user agent parsing ----------
            void ParseUserAgent(string ua, DateTime? clientCreatedTime)
            {
                userAgent = ua;
                createdTime = clientCreatedTime;
                userPreferredRegions = !ua.Contains("|N|", StringComparison.Ordinal);

                Match? info;
                if (ua.Contains("F ", StringComparison.Ordinal))
                {
                    // With 'F '
                    if (ua.Contains("|3.", StringComparison.Ordinal))
                    {
                        // Includes Direct package version
                        // cosmos-netstandard-sdk/3.22.0|3.22.0|1|X64|Linux SMP |.NET Core 3.1.20|N||F 00000001|
                        info = Regex.Match(ua,
                            @"cosmos-netstandard-sdk/(.*)\|.*\|.*\|(.*)\|(.*)\|(.*)\|(.*)\|(.*)\|");
                    }
                    else
                    {
                        // cosmos-netstandard-sdk/3.22.0|1|X64|Linux 4.14.35-linuxkit 1 SMP |.NET Core 3.1.20|N|F 00000001|
                        info = Regex.Match(ua,
                            @"cosmos-netstandard-sdk/(.*)\|.*\|(.*)\|(.*)\|(.*)\|(.*)\|(.*)\|");
                    }
                }
                else
                {
                    if (ua.Contains("|3.", StringComparison.Ordinal))
                    {
                        // cosmos-netstandard-sdk/3.22.0|3.22.0|1|X64|Linux ...|.NET Core 3.1.20|N
                        info = Regex.Match(ua,
                            @"cosmos-netstandard-sdk/(.*)\|.*\|.*\|(.*)\|(.*)\|(.*)\|(.*)\|(.*)");
                    }
                    else
                    {
                        // cosmos-netstandard-sdk/3.22.0|1|X64|Linux ...|.NET Core 3.1.20|N
                        info = Regex.Match(ua,
                            @"cosmos-netstandard-sdk/(.*)\|.*\|(.*)\|(.*)\|(.*)\|(.*)\|(.*)");
                    }
                }

                if (info.Success)
                {
                    version = info.Groups[1].Value;
                    // Based on Python: group(3) => environmentDescription, group(2) => platformDescription
                    platformDescription = info.Groups[2].Value;
                    environmentDescription = info.Groups[3].Value;
                }
            }

            // ---------- helpers: client config parsing ----------
            void ParseClientConfig(JObject clientCfg)
            {
                // "User Agent" and "Client Created Time Utc"
                var ua = clientCfg.TryGetValue("User Agent", out var uaTok) ? uaTok?.ToString() : null;
                var created = clientCfg.TryGetValue("Client Created Time Utc", out var ctTok)
                    ? ParseUtc(ctTok?.ToString())
                    : null;
                if (!string.IsNullOrEmpty(ua))
                {
                    ParseUserAgent(ua!, created);
                }

                if (clientCfg.TryGetValue("NumberOfActiveClients", out var acTok))
                {
                    if (int.TryParse(acTok?.ToString(), out var acVal)) numberOfClients = acVal;
                }
                else if (clientCfg.TryGetValue("NumberOfClientsCreated", out var nccTok))
                {
                    if (int.TryParse(nccTok?.ToString(), out var nccVal)) numberOfClients = nccVal;
                }

                if (clientCfg.TryGetValue("ProcessorCount", out var pcTok))
                {
                    if (int.TryParse(pcTok?.ToString(), out var pcv)) processorCount = pcv;
                }

                // Gateway config "(cps:..., urto:..., p:..., httpf: ...)"
                try
                {
                    var gw = clientCfg.SelectToken("ConnectionConfig.gw")?.ToString();
                    if (!string.IsNullOrEmpty(gw))
                    {
                        var m = Regex.Match(gw, @"\(cps:(.*), urto:(.*), p:(.*), httpf: (.*)\)");
                        if (m.Success)
                        {
                            gwConfig = new ClientGatewayConfiguration(
                                m.Groups[1].Value.Trim(),
                                m.Groups[2].Value.Trim(),
                                !string.Equals(m.Groups[3].Value.Trim(), "False", StringComparison.OrdinalIgnoreCase),
                                !string.Equals(m.Groups[4].Value.Trim(), "False", StringComparison.OrdinalIgnoreCase)
                            );
                        }
                    }
                }
                catch { /* ignore */ }

                // RNTBD config "(cto: ..., icto: ..., mrpc: ..., mcpe: ..., erd: ..., pr: ...)"
                try
                {
                    var rntbd = clientCfg.SelectToken("ConnectionConfig.rntbd")?.ToString();
                    if (!string.IsNullOrEmpty(rntbd))
                    {
                        var m = Regex.Match(rntbd,
                            @"\(cto: (.*), icto: (.*), mrpc: (.*), mcpe: (.*), erd: (.*), pr: (.*)\)");
                        if (m.Success)
                        {
                            tcpConfig = new ClientTCPConfiguration(
                                m.Groups[1].Value.Trim(),
                                m.Groups[2].Value.Trim(),
                                m.Groups[3].Value.Trim(),
                                m.Groups[4].Value.Trim(),
                                m.Groups[6].Value.Trim(),
                                !string.Equals(m.Groups[5].Value.Trim(), "False", StringComparison.OrdinalIgnoreCase)
                            );
                        }
                    }
                }
                catch { /* ignore */ }

                // Consistency "(consistency: ..., prgns:[...])" or with apprgn
                try
                {
                    var cc = clientCfg.SelectToken("ConsistencyConfig")?.ToString();
                    if (!string.IsNullOrEmpty(cc))
                    {
                        Match m;
                        if (cc.Contains("apprgn", StringComparison.Ordinal))
                        {
                            m = Regex.Match(cc, @"\(consistency: (.*), prgns:\[(.*)\], apprgn: (.*)\)");
                        }
                        else
                        {
                            m = Regex.Match(cc, @"\(consistency: (.*), prgns:\[(.*)\]\)");
                        }
                        if (m.Success)
                        {
                            var cons = m.Groups[1].Value.Trim();
                            if (!string.Equals(cons, "NotSet", StringComparison.OrdinalIgnoreCase))
                            {
                                consistencyOverride = cons;
                            }
                            regionalConfiguration = m.Groups[2].Value.Trim();

                            if (m.Groups.Count > 3)
                            {
                                var appRgn = m.Groups[3].Value.Trim();
                                if (!string.IsNullOrEmpty(appRgn))
                                {
                                    regionalConfiguration = "AppRegion " + appRgn;
                                }
                            }
                        }
                    }
                }
                catch { /* ignore */ }

                // Other "(ed:..., be:...)"
                try
                {
                    var other = clientCfg.SelectToken("other")?.ToString();
                    if (!string.IsNullOrEmpty(other))
                    {
                        var m = Regex.Match(other, @"\(ed:(.*), be:(.*)\)");
                        if (m.Success)
                        {
                            // ed != False  => LimitToEndpoint = true ==> crossRegionalRequestsEnabled = false
                            if (!string.Equals(m.Groups[1].Value.Trim(), "False", StringComparison.OrdinalIgnoreCase))
                            {
                                crossRegionalRequestsEnabled = false;
                            }
                            // be != False => bulk enabled
                            if (!string.Equals(m.Groups[2].Value.Trim(), "False", StringComparison.OrdinalIgnoreCase))
                            {
                                usingBulk = true;
                            }
                        }
                    }
                }
                catch { /* ignore */ }
            }

            // -------- First try to parse "new diagnostics" shape ----------
            try
            {
                operation = parsedJson.Value<string>("name");
                totalLatencyMs = parsedJson.Value<int?>("duration in milliseconds");

                var clientCfg = parsedJson.SelectToken("data['Client Configuration']") as JObject
                                ?? parsedJson.SelectToken("data.Client Configuration") as JObject;
                if (clientCfg != null)
                {
                    ParseClientConfig(clientCfg);
                }
            }
            catch
            {
                // Old diagnostics: try to find user agent text in raw json
                var rawJson = JsonConvert.SerializeObject(parsedJson);
                var start = rawJson.IndexOf("cosmos-netstandard-sdk", StringComparison.Ordinal);
                if (start >= 0)
                {
                    var end = rawJson.IndexOf('"', start);
                    if (end == -1) end = rawJson.Length;
                    var ua = rawJson.Substring(start, end - start);
                    ParseUserAgent(ua, null);
                }
            }

            var clientConfig = new ClientConfiguration(
                userAgent ?? string.Empty,
                version ?? string.Empty,
                environmentDescription,
                platformDescription,
                numberOfClients,
                crossRegionalRequestsEnabled,
                consistencyOverride,
                userPreferredRegions,
                usingBulk,
                createdTime,
                gwConfig,
                tcpConfig,
                regionalConfiguration,
                processorCount
            );

            // ---------------- CPU history parsing ----------------
            var cpuHistory = new List<CPUHistory>();

            void VerifyCpuInfo(JToken cpuInfoNode)
            {
                foreach (var node in cpuInfoNode.Children<JObject>())
                {
                    // require numeric cpu and memory
                    if (!node.TryGetValue("cpu", out var cpuTok) ||
                        !node.TryGetValue("memory", out var memTok))
                    {
                        continue;
                    }
                    if (!double.TryParse(cpuTok.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var cpuVal))
                        continue;
                    if (!double.TryParse(memTok.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var memVal))
                        continue;

                    ThreadInfo? threadInfo = null;
                    var ti = node["threadInfo"] as JObject;
                    if (ti != null)
                    {
                        try
                        {
                            var starving = string.Equals(ti.Value<string>("isThreadStarving"), "True", StringComparison.OrdinalIgnoreCase);
                            threadInfo = new ThreadInfo(
                                starving,
                                ti.Value<int>("minThreads"),
                                ti.Value<int>("maxThreads"),
                                ti.Value<int>("availableThreads"),
                                ti.Value<int>("threadWaitIntervalInMs")
                            );
                        }
                        catch { /* ignore */ }
                    }

                    var dt = ParseUtc(node.Value<string>("dateUtc"));
                    if (dt == null) continue;

                    cpuHistory.Add(new CPUHistory(cpuVal, memVal, dt.Value, threadInfo));
                }
            }

            // Older CPU history string: "(<time> <cpu>), (...)" etc.
            void VerifyOldCpuInfo(JToken cpuInfoNode)
            {
                var text = cpuInfoNode.ToString();
                var measurements = text.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var m in measurements)
                {
                    var match = Regex.Match(m, @"\((.*) (.*)\)");
                    if (!match.Success) continue;

                    var when = ParseUtc(match.Groups[1].Value);
                    if (when == null) continue;
                    if (!double.TryParse(match.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var cpuVal))
                        continue;

                    cpuHistory.Add(new CPUHistory(cpuVal, 0, when.Value, null));
                }
            }

            HelperFunctions.WalkJsonUntil(parsedJson, "systemHistory", VerifyCpuInfo);
            HelperFunctions.WalkJsonUntil(parsedJson, "CPU History", VerifyOldCpuInfo);

            // ---------------- Regions contacted ----------------
            void VerifyCrossRegionCalls(JToken node)
            {
                // Newer SDKs: number; older: array
                if (node.Type == JTokenType.Integer)
                {
                    contactedRegions = node.Value<int>();
                }
                else if (node is JArray arr)
                {
                    contactedRegions = arr.Count;
                }
            }
            HelperFunctions.WalkJsonUntil(parsedJson, "RegionsContacted", VerifyCrossRegionCalls);

            // ---------------- Network analysis (TCP + HTTP) ----------------
            var tcpRequests = new List<TCPRequest>();
            var httpRequests = new List<HTTPRequest>();

            void AnalyzeNetwork(JToken networkNode)
            {
                // TCP requests
                try
                {
                    var storeStats = networkNode["StoreResponseStatistics"] as JArray;
                    if (storeStats != null)
                    {
                        foreach (var stat in storeStats.OfType<JObject>())
                        {
                            var responseTime = ParseUtc(stat.Value<string>("ResponseTimeUTC")) ?? DateTime.UtcNow;

                            var storeResultTok = stat["StoreResult"];
                            if (storeResultTok == null) continue;

                            if (storeResultTok.Type == JTokenType.String)
                            {
                                // Old diagnostics, parse the big line
                                var storeResult = storeResultTok.ToString();
                                var m = Regex.Match(storeResult,
                                    @"StorePhysicalAddress: rntbd://(.*):(.*)/apps/.*services/.*partitions/(.*)/replicas/(.*)/.*StatusCode: (.*), SubStatusCode: (.*), RequestCharge.*TransportException: (.*)");
                                if (!m.Success)
                                {
                                    // Could not parse; skip
                                }
                                else
                                {
                                    var isTimeout = !m.Groups[7].Value.StartsWith("null", StringComparison.OrdinalIgnoreCase);

                                    tcpRequests.Add(new TCPRequest(
                                        time: responseTime,
                                        endpoint: m.Groups[1].Value,
                                        port: SafeInt(m.Groups[2].Value),
                                        partition: m.Groups[3].Value,
                                        replica: TrimTrailingSlash(m.Groups[4].Value),
                                        status: m.Groups[5].Value,
                                        substatus: m.Groups[6].Value,
                                        belatency: 0,
                                        istimeout: isTimeout,
                                        operationType: stat.Value<string>("OperationType") ?? string.Empty,
                                        activityId: stat.Value<string>("ActivityId") ?? string.Empty
                                    ));
                                }
                            }
                            else if (storeResultTok is JObject storeResultObj)
                            {
                                // New diagnostics JSON
                                var addr = storeResultObj.Value<string>("StorePhysicalAddress") ?? string.Empty;
                                var am = Regex.Match(addr, @"rntbd://(.*):(.*)/apps/.*services/.*partitions/(.*)/replicas/(.*)");
                                if (am.Success)
                                {
                                    var replica = TrimTrailingSlash(am.Groups[4].Value);
                                    var request = new TCPRequest(
                                        time: responseTime,
                                        endpoint: am.Groups[1].Value,
                                        port: SafeInt(am.Groups[2].Value),
                                        partition: am.Groups[3].Value,
                                        replica: replica.Length > 0 ? replica[..^1] : replica, // Python trimmed an extra char
                                        status: storeResultObj.Value<string>("StatusCode") ?? string.Empty,
                                        substatus: storeResultObj.Value<string>("SubStatusCode") ?? string.Empty,
                                        istimeout: storeResultObj["TransportException"] is JToken transportException && transportException.Type != JTokenType.Null,
                                        belatency: (double?)storeResultObj["BELatencyInMs"] ?? 0,
                                        operationType: stat.Value<string>("OperationType") ?? string.Empty,
                                        activityId: storeResultObj.Value<string>("ActivityId") ?? string.Empty
                                    );

                                    try
                                    {
                                        var tl = storeResultObj.SelectToken("transportRequestTimeline.requestTimeline") as JArray;
                                        if (tl != null)
                                        {
                                            var events = new List<object>();
                                            foreach (var ev in tl.OfType<JObject>())
                                            {
                                                events.Add(new List<object>
                                            {
                                                ev.Value<string>("event") ?? string.Empty,
                                                ev.Value<double?>("durationInMs") ?? 0d
                                            });
                                            }
                                            request.SetTcpTimeline(events);
                                        }
                                    }
                                    catch { /* ignore */ }

                                    tcpRequests.Add(request);
                                }
                            }
                        }
                    }
                }
                catch { /* ignore */ }

                // HTTP requests
                try
                {
                    var httpStats = networkNode["HttpResponseStats"] as JArray;
                    if (httpStats != null)
                    {
                        foreach (var hr in httpStats.OfType<JObject>())
                        {
                            var startTime = ParseUtc(hr.Value<string>("StartTimeUTC")) ?? DateTime.UtcNow;
                            double duration;
                            if (hr.TryGetValue("DurationInMs", out var dTok))
                            {
                                duration = SafeDouble(dTok?.ToString());
                            }
                            else
                            {
                                var end = ParseUtc(hr.Value<string>("EndTimeUTC")) ?? startTime;
                                duration = (end - startTime).TotalMilliseconds;
                            }

                            if (hr.ContainsKey("ExceptionType"))
                            {
                                var et = hr.Value<string>("ExceptionType") ?? string.Empty;
                                var isTimeout = et.Contains("TaskCanceled", StringComparison.OrdinalIgnoreCase)
                                                || et.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
                                                || et.Contains("The operation was canceled", StringComparison.OrdinalIgnoreCase);

                                httpRequests.Add(new HTTPRequest(
                                    time: startTime,
                                    duration: duration,
                                    url: hr.Value<string>("RequestUri") ?? string.Empty,
                                    resourcetype: hr.Value<string>("ResourceType") ?? string.Empty,
                                    operation: hr.Value<string>("HttpMethod") ?? string.Empty,
                                    statusCode: string.Empty,
                                    subStatusCode: string.Empty,
                                    activityId: hr.Value<string>("ActivityId") ?? string.Empty,
                                    istimeout: isTimeout
                                ));
                            }
                            else
                            {
                                var sub = hr.TryGetValue("SubStatusCode", out var subTok) ? subTok?.ToString() : null;
                                httpRequests.Add(new HTTPRequest(
                                    time: startTime,
                                    duration: duration,
                                    url: hr.Value<string>("RequestUri") ?? string.Empty,
                                    resourcetype: hr.Value<string>("ResourceType") ?? string.Empty,
                                    operation: hr.Value<string>("HttpMethod") ?? string.Empty,
                                    statusCode: hr.Value<string>("StatusCode") ?? string.Empty,
                                    subStatusCode: sub ?? string.Empty,
                                    activityId: hr.Value<string>("ActivityId") ?? string.Empty,
                                    istimeout: false
                                ));
                            }
                        }
                    }
                }
                catch { /* ignore */ }
            }

            HelperFunctions.WalkJsonUntil(parsedJson, "Client Side Request Stats", AnalyzeNetwork);

            // Point Operation Statistics: collect failed ActivityIds
            void AnalyzeActivityIds(JToken node)
            {
                try
                {
                    var obj = node as JObject;
                    if (obj == null) return;

                    var errorMessage = node.Value<string>("ErrorMessage");
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        var activityId = node.Value<string>("ActivityId") ?? string.Empty;
                        var statusCode = node.Value<string>("StatusCode") ?? string.Empty;

                        obj.TryGetValue("SubStatusCode", out var sst);
                        var subStatus = sst?.ToString() ?? string.Empty;

                        httpRequests.Add(new HTTPRequest(
                            time: DateTime.UtcNow, // Python used None; C# needs a value
                            duration: 0,
                            url: node.Value<string>("RequestUri") ?? string.Empty,
                            resourcetype: string.Empty,
                            operation: string.Empty,
                            statusCode: statusCode,
                            subStatusCode: subStatus ?? string.Empty,
                            activityId: activityId,
                            istimeout: false
                        ));
                    }
                }
                catch { /* ignore */ }
            }

            HelperFunctions.WalkJsonUntil(parsedJson, "Point Operation Statistics", AnalyzeActivityIds);

            // Build diagnostics and produce RCA
            var diagnostics = new Diagnostics(
                traceKustoQueries: TraceKusto,
                operation: operation ?? string.Empty,
                totalLatencyMs: totalLatencyMs ?? 0,
                contactedRegions: contactedRegions,
                clientConfig: clientConfig,
                cpuHistoryArray: cpuHistory,
                tcpRequests: tcpRequests,
                httpRequests: httpRequests
            );

            diagnostics.StatusSubstatus = HelperFunctions.BuildStatusFromRequests(httpRequests, tcpRequests);

            var rca = diagnostics.GenerateFinalRCA();

            // Special CancellationToken case if no issues found but string contains "Operation Cancelled Exception"
            if (!rca.FoundAnyIssues &&
                JsonConvert.SerializeObject(parsedJson).Contains("Operation Cancelled Exception", StringComparison.OrdinalIgnoreCase))
            {
                rca = new ComponentRCA(
                    routingTeam: "Customer",
                    description: "The user passed a CancellationToken that canceled the retries within the SDK. This scenario can be treated as a timeout and retried by the customer. The time assigned to the CancellationToken should be verified to be at least greater or equal to the RequestTimeout",
                    recommendation: "Follow up with the customer and share the details.",
                    publicRecommendation: "A CancellationToken was passed that canceled the retries within the SDK. This scenario can be treated as a timeout and retried. The time assigned to the CancellationToken should be verified to be at least greater or equal to the RequestTimeout",
                    relatedDocumentationLinks: new List<string> { "https://aka.ms/cosmosdb-tsg-request-timeout#cancellationtoken" },
                    foundIssues: true
                );
            }

            return new SDKDiagnosticsAnalysisResult(null, rca);
        }

        // ---------------- small helpers ----------------
        private static DateTime? ParseUtc(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
            {
                return dt.ToUniversalTime();
            }
            return null;
        }

        private static int SafeInt(string? s)
        {
            if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            return 0;
        }

        private static double SafeDouble(string? s)
        {
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            return 0d;
        }

        private static string TrimTrailingSlash(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.EndsWith("/", StringComparison.Ordinal) ? s[..^1] : s;
        }
    }
    public class JavaDiagnosticsAnalysis
    {
        public bool TraceKusto { get; set; }

        public JavaDiagnosticsAnalysis(bool traceKusto)
        {
            TraceKusto = traceKusto;
        }

        public SDKDiagnosticsAnalysisResult Analyze(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return new SDKDiagnosticsAnalysisResult("error or diagnostics are empty", null);
            }

            // --- Early, known RCAs ---
            if (error.Contains("SSLHandshakeException: PKIX path building failed", StringComparison.Ordinal))
            {
                return new SDKDiagnosticsAnalysisResult(
                    null,
                    new ComponentRCA(
                        routingTeam: "Customer",
                        description: "Starting July 2022, Azure Cosmos DB TLS server certificates will be issued by new Root and Intermediate Certificate Authorities. Your application is using an explicit list of CAs.",
                        recommendation: "Please share the information with the customer",
                        publicRecommendation: "Starting July 2022, Azure Cosmos DB TLS server certificates will be issued by new Root and Intermediate Certificate Authorities. Your application is using an explicit list of CAs, please follow the provided links.",
                        relatedDocumentationLinks: new List<string> {
                            "https://devblogs.microsoft.com/cosmosdb/tls-certificates-changes/"
                        },
                        foundIssues: true
                    )
                );
            }

            if (error.Contains("UnknownHostException", StringComparison.Ordinal))
            {
                return new SDKDiagnosticsAnalysisResult(
                    null,
                    new ComponentRCA(
                        routingTeam: "Customer",
                        description: "UnknownHostException detected",
                        recommendation: "Follow up with the customer and share the details.",
                        publicRecommendation: "Logs indicate UnknownHostException, please follow up with the documentation links.",
                        relatedDocumentationLinks: new List<string> {
                            "https://docs.microsoft.com/azure/cosmos-db/sql/troubleshoot-service-unavailable-java-sdk-v4-sql#unknownhostexception"
                        },
                        foundIssues: true
                    )
                );
            }

            // ---- Parse diagnostics string into JObject following Python cases ----
            object ParseDiagnosticsString(string diagnosticString)
            {
                var parsedDiagnostics = diagnosticString;

                // Escaped diagnostics: \"azsdk-java-cosmos...
                if (parsedDiagnostics.Contains("\\\"azsdk-java-cosmos", StringComparison.Ordinal))
                {
                    parsedDiagnostics = parsedDiagnostics.Replace("\\\"", "\"");
                }

                if (parsedDiagnostics.Contains("\"cosmosDiagnostics\":", StringComparison.Ordinal))
                {
                    // Extract value after "cosmosDiagnostics":
                    var start = parsedDiagnostics.IndexOf("\"cosmosDiagnostics\":", StringComparison.Ordinal)
                                + "\"cosmosDiagnostics\":".Length;
                    var end = parsedDiagnostics.IndexOf("\"\"}", start, StringComparison.Ordinal); // '""}' doesn’t occur; Python used '""}}' pattern
                    if (end == -1) end = parsedDiagnostics.IndexOf("\"}}", start, StringComparison.Ordinal);
                    if (end > -1)
                    {
                        end += 3; // include '}}'
                        parsedDiagnostics = parsedDiagnostics.Substring(start, end - start);
                    }
                    else
                    {
                        parsedDiagnostics = parsedDiagnostics.Substring(start);
                        if (!parsedDiagnostics.EndsWith("}", StringComparison.Ordinal))
                            parsedDiagnostics = parsedDiagnostics[..^1];
                    }
                }
                else if (parsedDiagnostics.Contains("{\"userAgent\":\"azsdk-java-cosmos", StringComparison.Ordinal))
                {
                    var start = parsedDiagnostics.IndexOf("{\"userAgent\":\"azsdk-java-cosmos", StringComparison.Ordinal);
                    var end = parsedDiagnostics.IndexOf("])\"}}", start, StringComparison.Ordinal);
                    if (end > -1)
                    {
                        end += 5; // include '])"}}'
                        parsedDiagnostics = parsedDiagnostics.Substring(start, end - start);
                    }
                    else
                    {
                        parsedDiagnostics = parsedDiagnostics.Substring(start);
                        if (!parsedDiagnostics.EndsWith("}", StringComparison.Ordinal))
                            parsedDiagnostics = parsedDiagnostics[..^1];
                    }
                }
                else if (parsedDiagnostics.Contains("ActivityId", StringComparison.Ordinal))
                {
                    // Build minimal JSON if we only have an ActivityId somewhere
                    var activityId = Util.ParseActivityId(parsedDiagnostics);
                    if (!string.IsNullOrEmpty(activityId))
                    {
                        var synthetic = new JObject
                        {
                            ["gatewayStatistics"] = new JObject
                            {
                                ["activityId"] = activityId,
                                ["statusCode"] = "None",
                                ["resourceType"] = "Document",
                                ["operationType"] = "Read"
                            },
                            ["responseStatisticsList"] = new JArray(),
                            ["regionsContacted"] = new JArray(),
                            ["requestLatencyInMs"] = 0
                        };
                        parsedDiagnostics = synthetic.ToString(Formatting.None);
                    }
                    else
                    {
                        return new SDKDiagnosticsAnalysisResult("Cannot find any activityId in the details", null);
                    }
                }

                try
                {
                    var token = JsonConvert.DeserializeObject(parsedDiagnostics);
                    if (token is JObject or JArray) return token!;
                    throw new JsonReaderException("No valid input");
                }
                catch (JsonReaderException)
                {
                    try
                    {
                        // Relax newline/escape issues similar to Python’s unicode_escape roundtrip
                        var compact = Regex.Replace(parsedDiagnostics, @"\r?\n", " ");
                        var token = JsonConvert.DeserializeObject(compact);
                        if (token is JObject or JArray) return token!;
                        throw new JsonReaderException("No valid input (second attempt)");
                    }
                    catch (JsonReaderException e2)
                    {
                        return new SDKDiagnosticsAnalysisResult(
                            $"Could not parse the exception details as a Java SDK Diagnostics {e2.Message}", null);
                    }
                }
            }

            var parsedObj = ParseDiagnosticsString(error);
            if (parsedObj is SDKDiagnosticsAnalysisResult fail)
                return fail;

            var parsedJson = parsedObj as JObject ?? (parsedObj is JArray arr && arr.Count > 0 && arr[0] is JObject jo ? jo : new JObject());

            if (!parsedJson.ContainsKey("responseStatisticsList") && !parsedJson.ContainsKey("gatewayStatistics"))
            {
                return new SDKDiagnosticsAnalysisResult(
                    "Could not parse the exception details as a Java SDK Diagnostics.", null);
            }

            var responseStatistics = parsedJson["responseStatisticsList"] as JArray;
            var gatewayStatistics = parsedJson["gatewayStatistics"] as JObject;

            string? operation = null;
            if (responseStatistics != null && responseStatistics.Type == JTokenType.Object &&
                responseStatistics["requestOperationType"] != null)
            {
                operation = responseStatistics.Value<string>("requestOperationType");
            }
            else if (gatewayStatistics != null && gatewayStatistics["operationType"] != null)
            {
                operation = gatewayStatistics.Value<string>("operationType");
            }

            var totalLatencyMs = parsedJson.Value<int?>("requestLatencyInMs") ?? 0;
            var contactedRegions = parsedJson["regionsContacted"] is JArray rc ? rc.Count : 0;

            var cpuHistory = new List<CPUHistory>();
            var tcpRequests = new List<TCPRequest>();
            var httpRequests = new List<HTTPRequest>();

            // --- CPU info ---
            void VerifyCpuInfo(JObject systemInfo)
            {
                var memory = systemInfo.Value<string>("usedMemory") ?? "0 KB";
                var sysCpu = systemInfo.Value<string>("systemCpuLoad") ?? "empty";
                if (!string.Equals(sysCpu, "empty", StringComparison.OrdinalIgnoreCase))
                {
                    // e.g. "[2024-05-05T12:34:56Z 12.3%], [time 7.1%]"
                    foreach (var entry in sysCpu.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var space = entry.IndexOf(' ');
                        if (space <= 0) continue;
                        var timeStr = entry.TrimStart('[').Substring(0, space - 1 + 1).Trim('[', ']');
                        var usageStr = entry.Substring(space + 1).TrimEnd(']').Trim();
                        if (!usageStr.EndsWith("%", StringComparison.Ordinal)) continue;

                        var time = ParseUtc(timeStr);
                        if (time == null) continue;

                        if (double.TryParse(usageStr.TrimEnd('%'), NumberStyles.Any, CultureInfo.InvariantCulture, out var cpu))
                        {
                            var memNum = ParseMemoryKb(memory); // remove " KB"
                            cpuHistory.Add(new CPUHistory(cpu, memNum, time.Value, null));
                        }
                    }
                }
            }

            if (parsedJson["systemInformation"] is JObject sysInfo)
            {
                VerifyCpuInfo(sysInfo);
            }

            // --- TCP ---
            void AnalyzeTcp(JArray storeResponseStatistics)
            {
                try
                {
                    foreach (var stat in storeResponseStatistics.OfType<JObject>())
                    {
                        var responseTime = ParseUtc(stat.Value<string>("requestResponseTimeUTC")) ?? DateTime.UtcNow;

                        var storeResult = stat["storeResult"] as JObject;
                        if (storeResult == null) continue;

                        var addr = storeResult.Value<string>("storePhysicalAddress") ?? string.Empty;
                        var m = Regex.Match(addr, @"rntbd://(.*):(.*)/apps/.*services/.*partitions/(.*)/replicas/(.*)");
                        if (!m.Success) continue;

                        var replica = TrimSlash(m.Groups[4].Value);
                        var request = new TCPRequest(
                            time: responseTime,
                            endpoint: m.Groups[1].Value,
                            port: SafeInt(m.Groups[2].Value),
                            partition: m.Groups[3].Value,
                            replica: replica.Length > 0 ? replica[..^1] : replica, // match Python's replica[:-1]
                            status: storeResult.Value<string>("statusCode") ?? string.Empty,
                            substatus: storeResult.Value<string>("subStatusCode") ?? string.Empty,
                            istimeout: storeResult["exceptionMessage"] != null &&
                                        storeResult.Value<int?>("statusCode") == 410,
                            belatency: storeResult.Value<object>("backendLatencyInMs") ?? 0,
                            operationType: stat.Value<string>("requestOperationType") ?? string.Empty,
                            activityId: string.Empty
                        );

                        try
                        {
                            var transport = storeResult["transportRequestTimeline"] as JArray;
                            var events = new List<object>();
                            if (transport != null)
                            {
                                foreach (var ev in transport.OfType<JObject>())
                                {
                                    var name = ev.Value<string>("eventName") ?? string.Empty;
                                    double durationMs = 0;
                                    if (ev["durationInMicroSec"] != null &&
                                        double.TryParse(ev["durationInMicroSec"]!.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var micro))
                                    {
                                        durationMs = micro / 1000.0;
                                    }
                                    if (ev["durationInMilliSecs"] != null &&
                                        double.TryParse(ev["durationInMilliSecs"]!.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var milli))
                                    {
                                        durationMs = milli;
                                    }
                                    events.Add(new List<object> { name, durationMs });
                                }
                            }
                            request.SetTcpTimeline(events);
                        }
                        catch { /* ignore */ }

                        tcpRequests.Add(request);
                    }
                }
                catch { /* ignore */ }
            }

            if (responseStatistics is { Count: > 0 })
            {
                AnalyzeTcp(responseStatistics);
            }

            // --- HTTP (gatewayStatistics) ---
            void AnalyzeHttp(JObject httpResponseStatistics)
            {
                try
                {
                    var startTime = DateTime.UtcNow;
                    double duration = 0;
                    var subStatusCode = "";
                    var activityId = "";

                    if (parsedJson["requestStartTimeUTC"] != null)
                    {
                        var st = ParseUtc(parsedJson.Value<string>("requestStartTimeUTC"));
                        if (st != null) startTime = st.Value;
                    }
                    if (parsedJson["requestLatencyInMs"] != null)
                    {
                        duration = SafeDouble(parsedJson["requestLatencyInMs"]!.ToString());
                    }
                    if (httpResponseStatistics["subStatusCode"] != null)
                    {
                        subStatusCode = httpResponseStatistics.Value<string>("subStatusCode") ?? "";
                    }
                    if (httpResponseStatistics["activityId"] != null)
                    {
                        activityId = httpResponseStatistics.Value<string>("activityId") ?? "";
                    }

                    httpRequests.Add(new HTTPRequest(
                        time: startTime,
                        duration: duration,
                        url: string.Empty, // not present in Java diagnostics
                        resourcetype: httpResponseStatistics.Value<string>("resourceType") ?? string.Empty,
                        operation: httpResponseStatistics.Value<string>("operationType") ?? string.Empty,
                        statusCode: httpResponseStatistics["statusCode"] != null
                                    ? httpResponseStatistics["statusCode"]!.ToString()
                                    : string.Empty,
                        subStatusCode: subStatusCode,
                        istimeout: false,
                        activityId: activityId
                    ));
                }
                catch { /* ignore */ }
            }

            if (gatewayStatistics is { Count: > 0 })
            {
                AnalyzeHttp(gatewayStatistics);
            }

            // --- Client configuration (parsed from the error string) ---
            string? userAgent = null, version = null, environmentDescription = null, platformDescription = null;
            int? numberOfClients = null, processorCount = null;
            bool? crossRegionalRequestsEnabled = null, userPreferredRegions = null, usingBulk = null;
            string? consistencyOverride = null, regionalConfiguration = null;
            ClientGatewayConfiguration? gwConfig = null;
            ClientTCPConfiguration? tcpConfig = null;
            DateTime? createdTime = null;

            void ParseClientConfig(string err)
            {
                const string target = "azsdk-java-cosmos/";
                var startUserAgent = err.IndexOf(target, StringComparison.Ordinal);
                if (startUserAgent != -1)
                {
                    var endUserAgent = err.IndexOf('"', startUserAgent);
                    if (endUserAgent != -1)
                    {
                        userAgent = err.Substring(startUserAgent, endUserAgent - startUserAgent);
                        // version + env/platform
                        var startVersion = userAgent.IndexOf(target, StringComparison.Ordinal);
                        var endVersion = userAgent.IndexOf(' ', startVersion);
                        if (startVersion != -1 && endVersion != -1)
                        {
                            version = userAgent.Substring(startVersion + target.Length, endVersion - (startVersion + target.Length));
                            var endEnvDesc = userAgent.IndexOf(' ', endVersion + 1);
                            if (endEnvDesc != -1)
                            {
                                environmentDescription = userAgent.Substring(endVersion + 1, endEnvDesc - (endVersion + 1));
                                platformDescription = userAgent.Substring(endEnvDesc + 1);
                            }
                        }
                    }
                }
                else
                {
                    return;
                }

                // clientCfgs object inside parsedJson (if present)
                var clientCfg = parsedJson["clientCfgs"] as JObject;
                if (clientCfg == null) return;

                numberOfClients = clientCfg.Value<int?>("numberOfClients");

                var connCfg = clientCfg["connCfg"] as JObject;
                if (connCfg != null)
                {
                    try
                    {
                        var other = connCfg.Value<string>("other");
                        if (!string.IsNullOrEmpty(other))
                        {
                            var m = Regex.Match(other, @"\(ed: (.*), cs: (.*)\)");
                            if (m.Success && !string.Equals(m.Groups[1].Value.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                            {
                                // ed != true => LimitToEndpoint == true => Cross-regional disabled
                                crossRegionalRequestsEnabled = false;
                            }
                        }
                    }
                    catch { /* ignore */ }

                    try
                    {
                        var consistencyCfg = clientCfg.Value<string>("consistencyCfg");
                        if (!string.IsNullOrEmpty(consistencyCfg))
                        {
                            var m = Regex.Match(consistencyCfg, @"\(consistency: (.*), mm: (.*), prgns: \[(.*)\]\)");
                            if (m.Success)
                            {
                                var cons = m.Groups[1].Value.Trim();
                                if (!string.Equals(cons, "NotSet", StringComparison.OrdinalIgnoreCase))
                                {
                                    consistencyOverride = cons;
                                }

                                var prgns = m.Groups[3].Value.Trim();
                                if (!string.Equals(prgns, "[]", StringComparison.Ordinal))
                                {
                                    regionalConfiguration = prgns;
                                }
                            }
                        }
                    }
                    catch { /* ignore */ }

                    try
                    {
                        var gw = connCfg.Value<string>("gw");
                        if (!string.IsNullOrEmpty(gw))
                        {
                            var m = Regex.Match(gw, @"\(cps:(.*), nrto:(.*), icto:(.*), p:(.*)\)");
                            if (m.Success)
                            {
                                gwConfig = new ClientGatewayConfiguration(
                                    m.Groups[1].Value.Trim(),
                                    m.Groups[2].Value.Trim(),
                                    !string.Equals(m.Groups[4].Value.Trim(), "false", StringComparison.OrdinalIgnoreCase),
                                    null // not in Java diagnostics
                                );
                            }
                        }
                    }
                    catch { /* ignore */ }

                    try
                    {
                        var rntbd = connCfg.Value<string>("rntbd");
                        if (!string.IsNullOrEmpty(rntbd))
                        {
                            var m = Regex.Match(rntbd, @"\(cto:(.*), nrto:(.*), icto:(.*), ieto:(.*), mcpe:(.*), mrpc:(.*), cer:(.*)\)");
                            if (m.Success)
                            {
                                tcpConfig = new ClientTCPConfiguration(
                                    requestTimeout: m.Groups[2].Value.Trim(),
                                    idleConnectionTimeout: m.Groups[3].Value.Trim(),
                                    maxRequestsPerChannel: m.Groups[6].Value.Trim(),
                                    maxConnectionsPerEndpoint: m.Groups[5].Value.Trim(),
                                    portReuseMode: string.Empty, // not present
                                    endpointRediscoveryEnabled: !string.Equals(m.Groups[7].Value.Trim(), "false", StringComparison.OrdinalIgnoreCase)
                                );
                            }
                        }
                    }
                    catch { /* ignore */ }
                }

                // "availableProcessors"
                const string procStr = "\"availableProcessors\": ";
                var sp = err.IndexOf(procStr, StringComparison.Ordinal);
                if (sp != -1)
                {
                    sp += procStr.Length;
                    var match = Regex.Match(err.Substring(sp), @"(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var cpuCnt))
                    {
                        processorCount = cpuCnt;
                    }
                }
            }

            ParseClientConfig(error);

            var clientConfig = new ClientConfiguration(
                userAgent: userAgent ?? string.Empty,
                version: version ?? string.Empty,
                environmentDescription: environmentDescription,
                platformDescription: platformDescription,
                numberOfClients: numberOfClients,
                crossRegionalRequestsEnabled: crossRegionalRequestsEnabled,
                consistencyOverride: consistencyOverride,
                usePreferredRegions: userPreferredRegions, // not in Java
                bulkMode: usingBulk,           // not in Java
                createdTime: createdTime,         // not in Java
                gwConfig: gwConfig,
                tcpConfig: tcpConfig,
                regionalConfiguration: regionalConfiguration,
                processorCount: processorCount
            );

            var diagnostics = new Diagnostics(
                traceKustoQueries: TraceKusto,
                operation: operation ?? string.Empty,
                totalLatencyMs: totalLatencyMs,
                contactedRegions: contactedRegions,
                clientConfig: clientConfig,
                cpuHistoryArray: cpuHistory,
                tcpRequests: tcpRequests,
                httpRequests: httpRequests
            );

            var rca = diagnostics.GenerateFinalRCA();

            // Special case: lease container invalid after monitored collection recreation
            if (!rca.FoundAnyIssues)
            {
                var start = error.IndexOf("PartitionKeyRangeGoneException", StringComparison.Ordinal);
                if (start != -1 && error.Contains("PartitionProcessorImpl", StringComparison.Ordinal))
                {
                    const string target = "\"innerErrorMessage\": \"The PartitionKeyRangeId: ";
                    var index = error.IndexOf(target, start, StringComparison.Ordinal);
                    if (index != -1)
                    {
                        var found = Regex.Match(error.Substring(index + target.Length), "\\\\\"(.*)\\\\\" is not valid for the current container");
                        if (found.Success)
                        {
                            rca = new ComponentRCA(
                                routingTeam: "Customer",
                                description: "Lease container state is invalid, customer recreated Monitored collection",
                                recommendation: "Follow up with the customer. Lease container needs to be deleted or existing related leases deleted",
                                publicRecommendation: "Logs indicate that the lease container state is invalid, the lease container needs to be recreated or existing related leases deleted..",
                                relatedDocumentationLinks: null,
                                foundIssues: true
                            );
                            return new SDKDiagnosticsAnalysisResult(null, rca);
                        }
                    }
                }
            }

            return new SDKDiagnosticsAnalysisResult(null, rca);
        }

        // ---- small helpers (local to this class) ----
        private static DateTime? ParseUtc(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
            {
                return dt.ToUniversalTime();
            }
            return null;
        }

        private static int SafeInt(string? s)
        {
            if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            return 0;
        }

        private static double SafeDouble(string? s)
        {
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            return 0d;
        }

        private static string TrimSlash(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.EndsWith("/", StringComparison.Ordinal) ? s[..^1] : s;
        }

        private static double ParseMemoryKb(string s)
        {
            // "12345 KB" -> 12345
            var cleaned = s.Trim();
            if (cleaned.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned[..^2].Trim();
            if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            return 0d;
        }
    }
}
