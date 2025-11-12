// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Framework;

namespace Agent.Plugins
{
    public class ControlFlowPluginDefinition
    {
        [Description("Waits for a specified amount of time. Other system events or user messages can interrupt a wait. In this case, you might need to resume the wait by calling the wait tool again, with a different duration")]
        public Task<string> Wait(
            [Description("The amount of time to wait in seconds")]
            int seconds)
        {
            throw new Exception("Control flow plugins should not be invoked directly.");
        }

        [Description("Used to indicate when no more agent actions are needed.")]
        public void MarkPlanComplete(
            [Description("The message to send to the user, indicating that the plan has been executed, summarizing the actions.")]
            string message)
        {
            throw new Exception("Control flow plugins should not be invoked directly.");
        }

        [Description("Sends the specified message to the user. Used this for cases where you would normally reply to the user instead of making a tool call. Do not use this if you need to wait for the user response, as this is a fire and forget notification.")]
        public void NotifyUser(
            [Description("The message to send to the user.")]
            string message)
        {
            throw new Exception("Control flow plugins should not be invoked directly.");
        }

        [Description("Sends the specified message to the user and indicates that you require a response to proceed. Only use this tool when you require important information and need to wait for the user response. Do not use this for any scenario where you just need to send the user an update in a fire and forget manner. If the user responds in a manner that does not satisfactorily answer your question, use this tool again.")]
        public void AskUserForInput(
            [Description("The question to ask the user.")]
            string message)
        {
            throw new Exception("Control flow plugins should not be invoked directly.");
        }


    }
}

