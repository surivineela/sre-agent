// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Core.Configuration
{
    // [OPTIONAL]
    // Expose an appsettings configuration section to control the behaviour of RevisionService which eventually helping to implement RevisionPlugin.
    public class RevisionSettings
    {
        public bool Enabled { get; set; } = true;
    }
}

