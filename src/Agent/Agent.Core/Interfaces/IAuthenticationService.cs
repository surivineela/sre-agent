// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Core;

namespace Agent.Core.Interfaces;

public interface IAuthenticationService
{
    /// <summary>
    /// Get the credential to access the document db
    /// </summary>
    /// <returns></returns>
    public TokenCredential GetDocumentDbCredential();

    /// <summary>
    /// Get the credential to access the dts
    /// </summary>
    /// <returns></returns>
    public TokenCredential GetDtsCredential();


    /// <summary>
    /// Get the credential to crawl resources of user tenant
    /// </summary>
    /// <returns></returns>
    public TokenCredential GetCrawlerCredential();

    /// <summary>
    /// Get the credential to operate on ARM resources
    /// </summary>
    /// <returns></returns>
    public TokenCredential GetArmOperationCredential();

    /// <summary>
    /// Get the credential to access the azure monitor workspace
    /// </summary>
    /// <returns></returns>
    public TokenCredential GetAzureMonitorWorkspaceCredential();

    /// <summary>
    /// Get the credential to access the azure open ai service
    /// </summary>
    /// <returns></returns>
    public TokenCredential GetAzureOpenAICredential();
}
