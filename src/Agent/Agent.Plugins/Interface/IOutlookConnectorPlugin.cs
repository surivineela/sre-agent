// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Agent.Plugins.Models;

namespace Agent.Plugins.Interface;

public interface IOutlookConnectorPlugin
{
    Task<EmailSendResult> SendEmailAsync(
        string to,
        string subject,
        string body,
        string bodyType,
        string importance,
        string? cc,
        string? bcc,
        CancellationToken cancellationToken = default);

    Task<EmailMessageResult> GetEmailAsync(
        string messageId,
        string? mailboxAddress,
        CancellationToken cancellationToken = default);

    Task<EmailListResult> ListEmailsAsync(
        string folderPath,
        bool fetchOnlyUnread,
        int top,
        string? mailboxAddress,
        CancellationToken cancellationToken = default);

    Task<EmailReplyResult> ReplyToEmailAsync(
        string messageId,
        string body,
        string bodyType,
        string importance,
        string? mailboxAddress,
        CancellationToken cancellationToken = default);

    Task<EmailMoveResult> MoveEmailAsync(
        string messageId,
        string destinationFolderPath,
        string? mailboxAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check connectivity to the Outlook service by making a lightweight API call.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A tuple where the first element indicates success (true) or failure (false), and the second element contains an empty string on success or a human-readable error message on failure.</returns>
    Task<(bool Success, string ErrorMessage)> CheckConnectivityAsync(CancellationToken cancellationToken = default);
}
