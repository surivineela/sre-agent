// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins
{
    public class TimePlugin : ITimePlugin
    {
        public DateTime GetCurrentUtcTime()
        {
            return DateTime.UtcNow;
        }
    }
}