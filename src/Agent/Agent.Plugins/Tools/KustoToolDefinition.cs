// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.Reasoning.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.IO;
namespace Agent.Plugins.Tools
{
    public enum KustoExecutionMode
    {
        Function,
        Query,
        Script
    }

    /// <summary>
    /// YAML tool definition for Kusto tools (functions, queries, scripts).
    /// </summary>
    public class KustoToolDefinition : YamlToolDefinitionBase
    {
        [YamlMember(Alias = "mode")]
        public KustoExecutionMode Mode { get; set; } = KustoExecutionMode.Function;

       

        [YamlMember(Alias = "function")]
        public string? Function { get; set; }

        [YamlMember(Alias = "query")]
        public string? Query { get; set; }

        [YamlMember(Alias = "file")]
        public string? File { get; set; }

        [YamlMember(Alias = "database")]
        public string Database { get; set; } = string.Empty;

        [YamlMember(Alias = "clusterHint")]
        public string? ClusterHint { get; set; }

        public KustoConnector GetConnector() => GetConnector<KustoConnector>();
        
        public override void Validate()
        {
            if (string.IsNullOrWhiteSpace(Database))
                throw new ArgumentException("Kusto tool must define a 'database'.");

            switch (Mode)
            {
                case KustoExecutionMode.Function:
                    if (string.IsNullOrWhiteSpace(Function))
                        throw new ArgumentException("Kusto tool in 'Function' mode must define a 'function'.");
                    break;

                case KustoExecutionMode.Query:
                    if (string.IsNullOrWhiteSpace(Query))
                        throw new ArgumentException("Kusto tool in 'Query' mode must define a 'query'.");
                    break;

                case KustoExecutionMode.Script:
                    if (string.IsNullOrWhiteSpace(File))
                        throw new ArgumentException("Kusto tool in 'Script' mode must define a 'file'.");
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported execution mode: {Mode}");
            }
        }
    }
}
