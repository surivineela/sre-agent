// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;

namespace Agent.Core.Interfaces;

public interface ISessionPoolService
{
    Task<string> ExecuteCliAsync(string command, string accessToken, string identifier);

    Task<SessionResponse> ExecuteShellCommandAsync(string command, string identifier);
}
