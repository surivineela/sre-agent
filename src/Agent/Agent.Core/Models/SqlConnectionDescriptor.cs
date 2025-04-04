// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Core.Models;

public enum ConnectionType
{
    None,
    Password,
    ManagedIdentity
}

public sealed record SqlConnectionDescriptor(
    [Description("SQL Server Address which App is connected to this App")]
    string SqlServerAddress,
    [Description("Azure SQL Server Resource Id connected to this App")]
    string SqlServerResourceId,
    [Description("Name of the database")]
    string DatabaseName,
    ConnectionType ConnectionType); 
