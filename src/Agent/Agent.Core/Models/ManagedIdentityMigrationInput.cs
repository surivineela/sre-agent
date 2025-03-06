using System.Collections.Generic;

namespace Agent.Core.Models;

public class ManagedIdentityMigrationInput
{
    public List<AppMigrationStatus> AppsToMigrate { get; set; }
}

public class AppMigrationStatus
{
    public string ResourceId { get; set; }
    public string Name { get; set; }
    public bool UsesAzureSqlConnectionString { get; set; }
    public string CurrentConnectionMethod { get; set; }
}
