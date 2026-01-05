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
}
