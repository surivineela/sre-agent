// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Configuration
{
    public class AgentModelSettings
    {
        public bool GPT5Enabled { get; set; } = false;

        public string AvailableModels { get; set; } = string.Empty;

        /// <summary>
        /// List of available models that can be used by the agent
        /// If empty, all registered models are considered available
        /// This is parsed from the AvailableModels string property which acts as a comma-separated list
        /// </summary>
        [JsonIgnore]
        public List<string> AvailableModelList => string.IsNullOrWhiteSpace(AvailableModels)
            ? new List<string>()
            : AvailableModels.Split(',')
                            .Select(m => m.Trim())
                            .Where(m => !string.IsNullOrWhiteSpace(m))
                            .Distinct()
                            .ToList();
    }
}
