// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using FirstPartyAgent.Core.Configuration;

namespace FirstPartyAgent.Helper;


public class FirstPartyAgentHelperSettings
{
    public OneBranchApprovalServiceSettings OneBranchApprovalService { get; set; } = new();
}
