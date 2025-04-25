// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models.Api.v1;
using Action = Agent.Core.Models.Api.v1.Action;

namespace Agent.Plugins.Definitions
{
    public class RecordActionsPluginDefinition
    {
        private readonly IRecordActionsPlugin _recordActionsPlugin;

        public RecordActionsPluginDefinition(IRecordActionsPlugin recordActionsPlugin)
        {
            _recordActionsPlugin = recordActionsPlugin ?? throw new ArgumentNullException(nameof(recordActionsPlugin));
        }

        [Description("Records an important action or decision made by the agent. ")]
        public async Task<Action> RecordAction(
            [Description("The thread ID this action is associated with")] Guid threadId,
            [Description("Title describing the action being recorded. Do not include status in it.")] string title,
            [Description("The name of the tool/function if the action is a tool call. Leave it to empty if it's not a tool call")] string toolName,
            [Description("Status of the action")] ActionStatus status,
            [Description("Severity of the action")] ActionSeverity severity)
        {
            return await _recordActionsPlugin.RecordAction(threadId, title, toolName, status, severity);
        }

        [Description("Retrieves information about a specific action")]
        public async Task<Action> GetActionDetails(
            [Description("The thread ID this action is associated with")] Guid threadId,
            [Description("The ID of the action to retrieve")] Guid actionId)
        {
            return await _recordActionsPlugin.GetAction(threadId, actionId);
        }
    }
}

