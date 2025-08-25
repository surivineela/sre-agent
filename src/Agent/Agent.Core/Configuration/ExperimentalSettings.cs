// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration
{
    public class ExperimentalSettings
    {
        public bool AutoHandoffToMeta { get; set; } = false;
        public bool EnableHandoffReasoning { get; set; } = false;
        public bool UseYamlForIncidentHandling { get; set; } = false;
    }
}

