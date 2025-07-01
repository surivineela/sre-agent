// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Interface;

public interface IWebAppPlugin
{
    Task<string> GetWebAppRebootWorkerDetails(string webappName, string stampName);
    Task<string> GetWebAppDetailsByName(string webappName);
    Task<string> GetWebAppHostnames(string webappName, string stampName);
}
