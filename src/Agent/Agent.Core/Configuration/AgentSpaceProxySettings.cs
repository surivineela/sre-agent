// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Core.Configuration
{
    public class AgentSpaceProxySettings
    {
        public string? Endpoint { get; set; }
        public string Scope { get; set; } = string.Empty;
    }
}
