// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Core.Models;

public class ManagedIdentityMigrationInput
{
    [Description("Apps which are not using Managed Identity for SQL MI Integration")]
    public required List<AppMigrationStatus> AppsToMigrate { get; set; }

    [Description("Detailed description of the issue.")]
    public required string message { get; set; }
}

public class AppMigrationStatus
{
    public required string ResourceId { get; set; }
    public required string Name { get; set; }
    public bool UsesAzureSqlConnectionString { get; set; }
    public required string CurrentConnectionMethod { get; set; }
}

