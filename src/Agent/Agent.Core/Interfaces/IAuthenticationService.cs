using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
}
