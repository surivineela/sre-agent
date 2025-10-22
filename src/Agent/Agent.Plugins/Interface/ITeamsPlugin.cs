// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Interface
{

    public record User(string Id, string DisplayName, string UserIdentityType, string TenantId);

    public record From(User User);

    public record Body(string ContentType, string Content);

    public record TeamsChannelMessage(string Id, string WebUrl, string Subject, From From, string MessageType, Body Body, DateTimeOffset? CreatedDateTime, DateTimeOffset? LastModifiedDateTime, DateTimeOffset? LastEditedDateTime);
    public record PostMessageResult(string Id, string WebUrl);

    public interface ITeamsPlugin
    {
        /// <summary>
        /// Send a message to a specific Teams channel specified in the connector settings
        /// </summary>
        /// <param name="message">message in html</param>
        Task<PostMessageResult> PostMessageToChannel(string subject, string message, CancellationToken cancellationToken = default);
        Task<List<TeamsChannelMessage>> GetMessagesFromChannel(CancellationToken cancellationToken = default);
    }
}
