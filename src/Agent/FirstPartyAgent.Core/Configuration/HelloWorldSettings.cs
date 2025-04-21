// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Core.Configuration
{
    // [OPTIONAL]
    // Expose an appsettings configuration section to control the behaviour of HelloWorldService which eventually helping to implement HelloWorldPlugin.
    // Example: Imagine this as IcMClientSettings required for creating an client instance of IcMClient and using it to expose to IcMClientPlugin.
    // Binding of this configuration section will be done in the Startup.cs file based passed 'firstPartyConfiguration' JSON configuration during Microsoft.App/Agents resource creation.
    public class HelloWorldSettings
    {
        public bool Enabled { get; set; } = true;
    }
}

