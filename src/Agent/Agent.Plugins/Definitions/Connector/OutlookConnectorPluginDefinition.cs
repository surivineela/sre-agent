// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;

namespace Agent.Plugins.Definitions.Connector;

[AgentToolPlugin(Category = ToolCategories.Utility, EnabledIf = "DataConnectorType:Outlook")]
public class OutlookConnectorPluginDefinition
{
    private readonly IOutlookConnectorPlugin _outlookConnectorPlugin;

    public OutlookConnectorPluginDefinition(IOutlookConnectorPlugin outlookConnectorPlugin)
    {
        _outlookConnectorPlugin = outlookConnectorPlugin;
    }

    [Description(@"Sends an email through the configured Outlook connector.
Use this whenever the user asks to send an email or perform an action that requires emailing.

When body_type is HTML (default), the `body` parameter MUST be well-formed, professional HTML:
- Do NOT use Markdown.
- Use a clear structure: greeting, body paragraphs, and closing.
- Use <p> for paragraphs, <ul>/<ol> and <li> for lists, and <a> for links.
- Use simple inline formatting only (<strong>, <em>, <br/>); avoid complex CSS or external assets.
- Keep a professional, concise tone and include a polite closing and signature when appropriate.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<EmailSendResult> SendOutlookEmail(
        [Description("Comma-separated list of primary recipients (To field).")]
        string to,
        [Description("Subject line for the email message.")]
        string subject,
        [Description("Body content for the email. Supports HTML when body_type is HTML.")]
        string body,
        [Description("Optional body type. Specify HTML or Text. Defaults to HTML."), DefaultValue("HTML")]
        string body_type = "HTML",
        [Description("Optional importance level. Accepted values: Low, Normal, High. Defaults to Normal."), DefaultValue("Normal")]
        string importance = "Normal",
        [Description("Optional comma-separated list of CC recipients.")]
        string? cc = null,
        [Description("Optional comma-separated list of BCC recipients.")]
        string? bcc = null)
    {
        return await _outlookConnectorPlugin.SendEmailAsync(
           to,
           subject,
           body,
           body_type,
           importance,
           cc,
           bcc).ConfigureAwait(false);
    }

    [Description(@"Retrieves a specific email message from the Outlook connector using the v2 Mail API. Provide the messageId returned by other email calls. Use mailbox_address when accessing a shared mailbox.

Note: Email body content is automatically truncated if it exceeds size limits (12,288 characters). Check IsBodyContentTruncated property to determine if content was truncated, and use BodyContentLength for the original content size. The ResponseContentSizeBytes property tracks the total response size.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<EmailMessageResult> GetOutlookEmail(
        [Description("Unique Outlook message identifier returned by list or trigger operations.")]
        string message_id,
        [Description("Optional mailbox address for shared mailboxes. Leave blank for the signed-in mailbox.")]
        string? mailbox_address = null)
    {
        return await _outlookConnectorPlugin.GetEmailAsync(
            message_id,
            mailbox_address).ConfigureAwait(false);
    }

    [Description(@"Lists messages from a specific folder via the Outlook v3 Mail endpoint. Set fetch_only_unread to true to filter unread messages. The top parameter is required and caps the number of messages returned (max 50). Use mailbox_address for shared mailboxes.

Note: To prevent context overflow, BodyContent is NOT included in list results - only metadata is returned. Use GetOutlookEmail with the message_id to retrieve the full email body (truncated to 12,288 characters). The ResponseContentSizeBytes property tracks the total response size.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<EmailListResult> ListOutlookEmails(
        [Description("Maximum number of messages to return (1-50). This value is required.")]
        int top,
        [Description("Folder path to query (e.g., Inbox, Sent Items)."), DefaultValue("Inbox")]
        string folder_path = "Inbox",
        [Description("When true, only unread messages are returned."), DefaultValue(false)]
        bool fetch_only_unread = false,
        [Description("Optional mailbox address for shared mailboxes. Leave blank for the signed-in mailbox.")]
        string? mailbox_address = null)
    {
        return await _outlookConnectorPlugin.ListEmailsAsync(
            folder_path,
            fetch_only_unread,
            top,
            mailbox_address).ConfigureAwait(false);
    }

    [Description(@"Replies to an existing email message via the Outlook v3 Mail ReplyTo endpoint. Provide HTML or text body content and optionally override importance or mailbox address.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<EmailReplyResult> ReplyToOutlookEmail(
        [Description("Unique Outlook message identifier to reply to.")]
        string message_id,
        [Description("Reply body content. Must be well-formed HTML when body_type is HTML.")]
        string body,
        [Description("Body type expected by Outlook. Accepted values: HTML or Text."), DefaultValue("HTML")]
        string body_type = "HTML",
        [Description("Importance level for the reply. Accepted values: Low, Normal, High."), DefaultValue("Normal")]
        string importance = "Normal",
        [Description("Optional mailbox address for shared mailboxes. Leave blank for the signed-in mailbox.")]
        string? mailbox_address = null)
    {
        return await _outlookConnectorPlugin.ReplyToEmailAsync(
            message_id,
            body,
            body_type,
            importance,
            mailbox_address).ConfigureAwait(false);
    }

    [Description(@"Moves an Outlook email message to a different folder. Provide the messageId and the destination folder path (e.g., Inbox/Processed for nested folders).")]
    [AgentTool(ToolMode.Auto)]
    public async Task<EmailMoveResult> MoveOutlookEmail(
        [Description("Unique Outlook message identifier to move.")]
        string message_id,
        [Description("Destination folder path, such as Inbox/Processed or Archive.")]
        string destination_folder_path)
    {
        return await _outlookConnectorPlugin.MoveEmailAsync(
            message_id,
            destination_folder_path).ConfigureAwait(false);
    }
}
