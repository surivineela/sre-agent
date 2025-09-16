// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

namespace Agent.Core.Configuration;

public class SessionPoolSettings
{
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Session pool management endpoint.
    /// </summary>
    public string PoolManagementEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Optional: Dedicated management endpoint for the "code interpreter" (python/report generation) session pool.
    /// If not set, fall back to <see cref="PoolManagementEndpoint"/>. This allows running code interpreter
    /// sessions in an isolated ACA pool with tighter egress / package controls.
    /// </summary>
    public string CodeInterpreterPoolManagementEndpoint { get; set; } = string.Empty;
}
