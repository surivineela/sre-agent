// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    public class TimePluginDefinition
    {
        private readonly ITimePlugin _timePlugin;

        public TimePluginDefinition(ITimePlugin timePlugin)
        {
            _timePlugin = timePlugin;
        }

        [KernelFunction("get_current_time")]
        [Description("Tells you the current time in UTC")]
        public DateTime GetCurrentUtcTime()
        {
            return _timePlugin.GetCurrentUtcTime();
        }
    }
}