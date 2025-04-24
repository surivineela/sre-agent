// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models.Api.v1;

namespace Agent.Plugins.Definitions
{
    public class ControlFlowV2PluginDefinition
    {
        private readonly IControlFlowV2Plugin _controlFlowV2Plugin;

        public ControlFlowV2PluginDefinition(IControlFlowV2Plugin controlFlowV2Plugin)
        {
            _controlFlowV2Plugin = controlFlowV2Plugin ?? throw new ArgumentNullException(nameof(controlFlowV2Plugin));
        }

        [Description("Starts a wait state")]
        public async Task StartWait(
            [Description("Reason for waiting")] string waitReason,
            [Description("Optional amount of time to wait for")] TimeSpan? waitFor = null)
        {
            await _controlFlowV2Plugin.StartWait(waitReason, waitFor);
        }

        [Description("Gets the current wait state if one exists")]
        public async Task<WaitInformation?> GetWaitState()
        {
            return await _controlFlowV2Plugin.GetWaitState();
        }

        [Description("Cancels the current wait state if one exists")]
        public async Task CancelWait()
        {
            await _controlFlowV2Plugin.CancelWait();
        }

        [Description("Completes the current agent context as it has reached a concluding state.")]
        public async Task Complete()
        {
            await _controlFlowV2Plugin.Complete();
        }

        [Description("Starts the approval flow by providing the user with an approval link for them to approve or reject the operation with.")]
        public async Task<ApprovalInformation> StartApprovalFlow([Description("Title of the approval flow")] string title)
        {
            return await _controlFlowV2Plugin.StartApprovalFlow(title);
        }

        [Description("Get the state of the approval flow that was previously started")]
        public Task<string> GetApprovalState()
        {
            return _controlFlowV2Plugin.GetApprovalState();
        }

        [Description("Asks the user for input and waits until it is provided")]
        public async Task AskForUserInput([Description("Message for the user, asking for the input that is required")] string message)
        {
            await _controlFlowV2Plugin.AskForUserInput(message);
        }

        [Description("Notifies the user about the current state. This is for providing updates, not for asking a question to the user.")]
        public Task NotifyUser([Description("Message for the user, to update on the current status")] string message)
        {
            return _controlFlowV2Plugin.NotifyUser(message);
        }
    }
}


