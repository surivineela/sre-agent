// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Plugins.Interface;
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

        [KernelFunction("get_app_timezone")]
        [Description("Returns the timezone string for an app hosted in various Azure data centers")]
        public string GetAppTimeZone([Description("Azure ResourceId of the app")] string resourceId)
        {
            return _timePlugin.GetAppTimeZone(resourceId);
        }
    }
}
