using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Plugins
{
    public class ControlFlowPluginDefinition
    {
        [Description("Waits for a specified amount of time")]
        public async Task<string> Wait(
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

        [Description("Sends the specified message to the user. Used this for cases where you would normally reply to the user instead of making a tool call.")]
        public void NotifyUser(
            [Description("The message to send to the user.")]
            string message)
        {
            throw new Exception("Control flow plugins should not be invoked directly.");
        }
    }
}
