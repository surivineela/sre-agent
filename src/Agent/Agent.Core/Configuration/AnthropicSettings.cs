// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration
{
    public class AnthropicSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        // For Microsoft Foundry, the BaseUrl looks like https://<your-foundry-instance>.services.ai.azure.com/anthropic/
        public string BaseUrl { get; set; } = string.Empty;
        public int MaxRetries { get; set; } = 3;
    }
}
