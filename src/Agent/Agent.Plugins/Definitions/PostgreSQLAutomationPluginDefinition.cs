using System.ComponentModel;
using System.Threading.Tasks;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    /// <summary>
    /// Definition for the PostgreSQL Automation Plugin
    /// </summary>
    [AgentToolPlugin]
    public class PostgreSQLAutomationPluginDefinition
    {
        private readonly IPostgreSQLAutomationPlugin _postgreSQLAutomationPlugin;

        /// <summary>
        /// Constructor for PostgreSQLAutomationPluginDefinition
        /// </summary>
        /// <param name="postgreSQLAutomationPlugin">The PostgreSQL Automation Plugin implementation</param>
        public PostgreSQLAutomationPluginDefinition(IPostgreSQLAutomationPlugin postgreSQLAutomationPlugin)
        {
            _postgreSQLAutomationPlugin = postgreSQLAutomationPlugin;
        }

        /// <summary>
        /// Executes PostgreSQL read commands safely with approval workflow
        /// </summary>
        [Description("Executes PostgreSQL read commands (SELECT, SHOW, DESCRIBE, EXPLAIN) safely with approval workflow. " +
                    "SECURITY: All operations require approval before execution for compliance. " +
                    "ALLOWED: SELECT statements, SHOW commands, DESCRIBE/\\d commands, EXPLAIN statements, information queries. " +
                    "FORBIDDEN: INSERT, UPDATE, DELETE, CREATE, DROP, ALTER operations for safety. " +
                    "WORKFLOW: Command validation → Approval request → Secure execution → Results. " +
                    "USE WHEN: Need to query PostgreSQL databases, inspect table structures, analyze query plans, or retrieve data.")]
        public async Task<CliToolExecutionResult> RunPsqlReadCommandAsync(
            [Description("The PostgreSQL command to execute (read-only operations only)")] string command,
            [Description("Optional database name to connect to")] string? database = null)
        {
            return await _postgreSQLAutomationPlugin.RunPsqlReadCommandAsync(command, database);
        }

        /// <summary>
        /// Validates PostgreSQL commands for safety and syntax
        /// </summary>
        [Description("Validates PostgreSQL commands for safety (read-only check) and basic syntax validation. " +
                    "VALIDATION: Checks for forbidden write operations, validates SQL syntax, ensures command safety. " +
                    "SECURITY: Prevents dangerous operations before execution. " +
                    "USE WHEN: Need to pre-validate commands before submission or provide syntax feedback.")]
        public async Task<string?> ValidatePsqlCommandAsync(
            [Description("The PostgreSQL command to validate")] string command,
            [Description("Optional database name to connect to")] string? database = null)
        {
            return await _postgreSQLAutomationPlugin.ValidatePsqlCommandAsync(command, database);
        }
    }
}
