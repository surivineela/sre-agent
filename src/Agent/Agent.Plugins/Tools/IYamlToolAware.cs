// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.Reasoning.Models;

namespace Agent.Plugins.Tools
{
    public interface IYamlToolAware
    {
        void SetToolDefinition(YamlToolDefinitionBase definition);
    }
}
