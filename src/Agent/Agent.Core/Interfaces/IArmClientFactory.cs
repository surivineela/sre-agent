// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Core;
using Azure.ResourceManager;

namespace Agent.Core.Interfaces;

public interface IArmClientFactory
{

    /// <summary>
    /// Get Arm client for generic purpose for arm operations
    /// </summary>
    /// <returns></returns>
    public ArmClient GetArmClient();

    /// <summary>
    /// Get Arm client for generic purpose for arm operations with specific credential. Responsibility to dispose the client is on the caller.
    /// </summary>
    /// <param name="cred"></param>
    /// <returns></returns>
    public ArmClient GetArmClient(TokenCredential cred);

    /// <summary>
    /// Get Arm client for crawling
    /// </summary>
    /// <returns></returns>
    public ArmClient GetCrawlerArmClient();
    
}

