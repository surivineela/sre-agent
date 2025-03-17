using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Models.Api.v1
{
    public record Thread(
        Guid Id,
        string Title,
        Message StartMessage,
        DateTime CreatedTimestamp,
        DateTime ModifiedTimestamp);

    public record CreateThreadRequest(
        [Required] CreateMessageRequest StartMessage
    );

    public record CreateMessageRequest(
        [Required] string Text,
        string UserId,
        string DisplayName
    );
}
