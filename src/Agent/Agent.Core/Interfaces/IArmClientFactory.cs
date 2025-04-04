// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

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
    /// Get Arm client for crawling
    /// </summary>
    /// <returns></returns>
    public ArmClient GetCrawlerArmClient();
    
}

