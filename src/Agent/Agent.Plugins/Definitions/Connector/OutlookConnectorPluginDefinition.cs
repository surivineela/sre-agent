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
}
