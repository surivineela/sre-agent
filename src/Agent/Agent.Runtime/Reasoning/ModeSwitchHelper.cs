// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.Reasoning;

internal static class ModeSwitchHelper
{

    internal static bool ModeSwitchEnabled(CoreSettings coreSettings) =>
        coreSettings.Experimental?.EnableModeSwitch ?? false;

    internal static string GetRcaRootAgent(AgentContext context, CoreSettings coreSettings)
    {
        if (ModeSwitchEnabled(coreSettings) && context.AgentMode == nameof(ReasoningMode.Conversation))
        {
            return RcaRoutingConstants.ConversationRootAgent;
        }
        return RcaRoutingConstants.WorkflowRootAgent;
    }

    internal static bool UseWorkflowOrchestrator(string agentType, AgentContext context, CoreSettings coreSettings)
    {
        if (agentType != RcaRoutingConstants.AgentType) return false;
        if (ModeSwitchEnabled(coreSettings) && context.AgentMode == nameof(ReasoningMode.Conversation))
        {
            // in conversation mode we do not create orchestrator
            return false;
        }
        return true;
    }
}
