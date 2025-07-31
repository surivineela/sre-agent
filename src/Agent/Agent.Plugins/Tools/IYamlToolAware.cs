// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;

namespace Agent.Plugins.Tools
{
    public interface IYamlToolAware
    {
        void SetToolDefinition(YamlToolDefinitionBase definition);
    }
}
