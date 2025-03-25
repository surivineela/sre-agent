using System.Collections.Generic;
using System.ComponentModel;

namespace Agent.Core.Models;

public class ManagedIdentityMigrationInput
{
    [Description("Apps which are not using Managed Identity for SQL MI Integration")]
    public List<AppMigrationStatus> AppsToMigrate { get; set; }

    [Description("Detailed description of the issue.")]
    public string message { get; set; }
}

public class AppMigrationStatus
{
    public string ResourceId { get; set; }
    public string Name { get; set; }
    public bool UsesAzureSqlConnectionString { get; set; }
    public string CurrentConnectionMethod { get; set; }
}
