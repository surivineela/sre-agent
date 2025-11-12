// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Framework;
using YamlDotNet.Serialization;

namespace Agent.Data.Tools
{
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

        [YamlMember(Alias = "cluster_hint")]
        public string? ClusterHint { get; set; }

        [YamlMember(Alias = "print_query")]
        public bool PrintQuery { get; set; } = true;

        [YamlMember(Alias = "display_options")]
        public KustoDisplayOptionsDefinition? DisplayOptions { get; set; }

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

            if (DisplayOptions is not null)
            {
                DisplayOptions.Validate();
            }
        }
    }
}
