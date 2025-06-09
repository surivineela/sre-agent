using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace Agent.Data.DataModels
{
    public record KubectlExecutionDocument(
        string Id,
        string ThreadId,
        string Command,
        string Stdin,
        string Description,
        KubectlExecutionStatus Status,
        string ClusterResourceId,
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
        public string DocumentType => "KubectlExecution";
        public string PartitionKey => ThreadId;
        public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

        public static KubectlExecutionDocument FromDomainModel(KubectlExecution execution, string threadId)
        {
            return new KubectlExecutionDocument(
                execution.Id.ToString(),
                threadId,
                execution.Command,
                execution.Stdin ?? string.Empty,
                execution.Description,
                execution.Status,
                execution.ClusterResourceId,
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

        public KubectlExecution ToDomainModel()
        {
            return new KubectlExecution(
                Guid.Parse(Id),
                Command,
                Stdin,
                Description,
                Status,
                ClusterResourceId,
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
    }
}
