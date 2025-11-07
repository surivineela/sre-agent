using Agent.Core.Models.Api.v1;

namespace Agent.Plugins.Interface;

public interface IPostgreSQLAutomationPlugin
{
    /// <summary>
    /// Gets or sets the thread context
    /// </summary>
    public Guid? ThreadId { get; set; }

    Task<CliToolExecutionResult> RunPsqlReadCommandAsync(string command, string? database = null);

    Task<string?> ValidatePsqlCommandAsync(string command, string? database = null);
}
