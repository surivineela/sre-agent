using System.ComponentModel;

namespace Agent.Plugins.Models
{
    public sealed record ManagedIdentityMigrationAnalysisDescriptor(
    [Description("Full GitHub repository URL. Can be inferred from app being CI/CD Enabled.Always confirm")] string repoUrl,
    [Description("Name of the branch to clone. Can be inferred from app's CI?CD Branch")] string branchToClone,
    [Description("Name of the branch to create with the fix.")] string branchName,
    [Description("SQLServer name in the original connection string. We are trying to migrate this to to use AD Based auth")] string sqlServer,
    [Description("Database in the original connection string")] string database);
}
