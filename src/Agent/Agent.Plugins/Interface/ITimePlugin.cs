// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Interface
{
    public interface ITimePlugin
    {
        DateTime GetCurrentUtcTime();
        string GetAppTimeZone(string resourceId);
    }
}
