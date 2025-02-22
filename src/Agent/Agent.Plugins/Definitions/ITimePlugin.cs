// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins
{
    public interface ITimePlugin
    {
        DateTime GetCurrentUtcTime();
    }
}
