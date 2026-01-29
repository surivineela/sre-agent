// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration;

/// <summary>
/// Configuration settings for Azure Dev Compute (ADC) sandbox integration.
/// Enables stateful remote sandbox environments with WebSocket exec/stream connectivity.
/// </summary>
public class AdcSettings
{
    /// <summary>
    /// Feature flag to enable/disable ADC integration.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// ADC management API base URL.
    /// </summary>
    public string BaseUrl { get; set; } = "https://management.azuredevcompute.io";

    /// <summary>
    /// Docker base image reference for disk image creation.
    /// </summary>
    public string BaseImage { get; set; } = "";

    /// <summary>
    /// Default CPU allocation (e.g., "4").
    /// </summary>
    public string DefaultCpu { get; set; } = "1";

    /// <summary>
    /// Default memory allocation (e.g., "8Gi").
    /// </summary>
    public string DefaultMemory { get; set; } = "2Gi";

    /// <summary>
    /// Default disk size (e.g., "50Gi").
    /// </summary>
    public string DefaultDisk { get; set; } = "25Gi";

    // Only used for local testing
    public string LocalSandboxEndpoint { get; set; } = "";

    /// <summary>
    /// Default ports to expose on sandboxes.
    /// Each entry specifies port number, optional name, and protocol (Http, Http2).
    /// </summary>
    public List<AdcPortConfig> DefaultPorts { get; set; } = new() { new AdcPortConfig { Port = 8080, Protocol = "Http2" } };

    /// <summary>
    /// Idle timeout in seconds before WebSocket connection is closed.
    /// Default: 300 seconds (5 minutes).
    /// </summary>
    public int IdleTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// GitHub token override for local development testing.
    /// When set in development mode, this token will be used instead of fetching from the token provider.
    /// </summary>
    public string GitHubTokenOverride { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for a port to expose on ADC sandboxes.
/// </summary>
public class AdcPortConfig
{
    /// <summary>
    /// Port number to expose.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Optional friendly name for the port.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Protocol: "Http" or "Http2".
    /// </summary>
    public string Protocol { get; set; } = "Http2";
}
