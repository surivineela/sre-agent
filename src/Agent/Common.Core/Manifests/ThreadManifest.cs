// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;

namespace Agent.Common.Core.Manifests
{
    /// <summary>
    /// YAML manifest wrapper for creating a Thread with an optional starting agent.
    /// apiVersion: azuresre.ai/v1
    /// kind: Thread
    /// </summary>
    public class ThreadManifest
    {
        public string ApiVersion { get; set; } = "azuresre.ai/v1";
        public string Kind { get; set; } = "Thread";
        public ManifestMetadata Metadata { get; set; } = new ManifestMetadata();
        public ThreadSpec Spec { get; set; } = new ThreadSpec();
    }

    public class ThreadSpec
    {
        public string Message { get; set; } = string.Empty;
        public string? Agent { get; set; } // starting agent
        public string? UserId { get; set; }
        public string? DisplayName { get; set; }
        public string Source { get; set; } = "Conversation";
    }
}
