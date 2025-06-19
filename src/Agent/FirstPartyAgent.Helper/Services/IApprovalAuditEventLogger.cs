// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using FirstPartyAgent.Helper.Models;

namespace FirstPartyAgent.Helper.Services
{
    public interface IApprovalAuditEventLogger
    {
        Task LogEventAsync(ApprovalAuditEvent auditEvent);
    }
}
