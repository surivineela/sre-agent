// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.Reasoning.Models;

namespace Agent.Plugins.Tools
{
    public class KustoToolDefinition : YamlToolDefinitionBase
    {
        public string Function { get; set; } = default!;

        public override void Validate()
        {
            if (string.IsNullOrWhiteSpace(Function))
                throw new ArgumentException("Kusto tool must define a 'function'.");
        }
    }

}

