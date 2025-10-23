// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration;

/// <summary>
/// Configuration settings for MDM (Microsoft Diagnostics Metrics) integration, both configurations are for local debugging purposes ONLY
/// </summary>
public class MdmMetricsSettings
{
    /// <summary>
    /// The client ID of the user-assigned managed identity for MDM access.
    /// If not specified, system-assigned managed identity will be used.
    /// Note: This is primarily for local debugging purposes. In production environments,
    /// the Action Identity will be used automatically by the deployed service.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// The endpoint URL for acquiring user tokens when UseUserToken is enabled.
    /// Only required when UseUserToken is true.
    /// </summary>
    public string? TokenEndpoint { get; set; }
}
