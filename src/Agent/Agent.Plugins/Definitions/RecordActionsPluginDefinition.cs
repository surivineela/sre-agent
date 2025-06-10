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

        [Description("Retrieves information about a specific action")]
        public async Task<Action> GetActionDetails(
            [Description("The thread ID this action is associated with")] Guid threadId,
            [Description("The ID of the action to retrieve")] Guid actionId)
        {
            return await _recordActionsPlugin.GetAction(threadId, actionId);
        }
    }
}

