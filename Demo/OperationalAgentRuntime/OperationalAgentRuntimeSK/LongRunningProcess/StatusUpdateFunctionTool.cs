using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OperationalAgentCore;

namespace OperationalAgentRuntimeSK.LongRunningProcess
{
    public class StatusUpdateFunctionTool
    {
        private readonly TeamsConnector teamsConnector;

        public StatusUpdateFunctionTool(TeamsConnector teamsConnector)
        {
            this.teamsConnector = teamsConnector;
        }

        [Description("Sends the user a status update during an ongoing operation")]
        public async Task SendStatusUpdate(
            [Description("The message to send the user. Supports emojis and other rich formatting, which you should use to emphasize important details such as resource ids and timestamps. For any message about change being made, (such as updating a configuration setting) it is important that you include a UTC timestamp.")]
            string message)
        {
            await teamsConnector.PostMessageAsync(new TeamsMessage(message));
            await Task.Delay(500);
        }
    }
}
