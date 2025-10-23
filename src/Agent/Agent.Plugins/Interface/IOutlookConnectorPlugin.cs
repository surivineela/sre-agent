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
}
