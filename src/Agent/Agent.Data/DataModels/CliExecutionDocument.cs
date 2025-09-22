using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace Agent.Data.DataModels
{
    public record CliExecutionDocument(
        string Id,
        string ThreadId,
        string Command,
        string Description,
        AzCliExecutionStatus Status,
        string? OriginalFunctionCall,
        string? Output,
        string? Error,
        DateTime CreatedTimestamp,
        DateTime? StartedTimestamp,
        DateTime? CompletedTimestamp,
        Author? ExecutedBy,
        string? AgentContextId
    ) : ICosmosDocument
    {
        public string DocumentType => "CliExecution";
        public string PartitionKey => ThreadId;
        public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

        public static CliExecutionDocument FromDomainModel(AzCliExecution execution, string threadId)
        {
            return new CliExecutionDocument(
                execution.Id.ToString(),
                threadId,
                execution.Command,
                execution.Description,
                execution.Status,
                execution.OriginalFunctionCall,
                execution.Output,
                execution.Error,
                execution.CreatedTimestamp,
                execution.StartedTimestamp,
                execution.CompletedTimestamp,
                execution.ExecutedBy != null
                    ? new Author(execution.ExecutedBy.Role, execution.ExecutedBy.UserId, execution.ExecutedBy.DisplayName)
                    : null,
                execution.AgentContextId?.ToString() ?? string.Empty
            );
        }

        public AzCliExecution ToDomainModel()
        {
            return new AzCliExecution(
                Guid.Parse(Id),
                Command,
                Description,
                Status,
                OriginalFunctionCall,
                Output,
                Error,
                CreatedTimestamp,
                StartedTimestamp,
                CompletedTimestamp,
                ExecutedBy,
                string.IsNullOrEmpty(AgentContextId) ? Guid.Empty : Guid.Parse(AgentContextId)
            );
        }

        public PsqlExecution ToPsqlDomainModel()
        {
            return new PsqlExecution(
                Guid.Parse(Id),
                Command,
                Description,
                Status,
                OriginalFunctionCall,
                Output,
                Error,
                CreatedTimestamp,
                StartedTimestamp,
                CompletedTimestamp,
                ExecutedBy,
                string.IsNullOrEmpty(AgentContextId) ? Guid.Empty : Guid.Parse(AgentContextId)
            );
        }

        public static CliExecutionDocument FromDomainModel(PsqlExecution execution, string threadId)
        {
             return new CliExecutionDocument(
                execution.Id.ToString(),
                threadId,
                execution.Command,
                execution.Description,
                execution.Status,
                execution.OriginalFunctionCall,
                execution.Output,
                execution.Error,
                execution.CreatedTimestamp,
                execution.StartedTimestamp,
                execution.CompletedTimestamp,
                execution.ExecutedBy != null
                    ? new Author(execution.ExecutedBy.Role, execution.ExecutedBy.UserId, execution.ExecutedBy.DisplayName)
                    : null,
                execution.AgentContextId?.ToString() ?? string.Empty
            );
        }
    }
}
