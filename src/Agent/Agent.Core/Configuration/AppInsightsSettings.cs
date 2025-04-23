// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{
    public class AppInsightsSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
    }
}
