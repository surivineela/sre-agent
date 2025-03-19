using Microsoft.Bot.Schema;
namespace Agent.Core.Models.Api.v1;
public record ThreadTeamsMapping(
    string Id,
    string ThreadId,
    string ConversationId,
    string ChannelId,
    string ServiceUrl,
    DateTime CreatedTimestamp,
    DateTime ModifiedTimestamp,
    ConversationReference? Reference = null
);
