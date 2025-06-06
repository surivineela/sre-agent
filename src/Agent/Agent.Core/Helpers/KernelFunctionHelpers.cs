// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Agent.Logging;

namespace Agent.Core.Helpers;

internal class KernelFunctionHelpers
{
    internal static TResult TryAction<TResult>(string className, Func<TResult> func, ILogger logger, [CallerMemberName] string caller = "Unknown")
    {
        try
        {
            logger.LogInternalInformation($"[{className}] Performing action '{caller}'...");
            TResult res = func();
            logger.LogInternalInformation($"[{className}] Completed action '{caller}'");
            return res;
        }
        catch (Exception e)
        {
            logger.LogInternalError(e, "Error occurred while executing action");
            throw;
        }
    }

    internal static (string owner, string repo) ParseGitHubUrl(string repoUrl)
    {
        var match = Regex.Match(repoUrl, @"github\.com[/:](?<owner>[\w.-]+)/(?<repo>[\w.-]+)(?:\.git)?$");
        if (!match.Success)
        {
            throw new ArgumentException("Invalid GitHub repository URL format");
        }

        return (match.Groups["owner"].Value, match.Groups["repo"].Value);
    }
}

