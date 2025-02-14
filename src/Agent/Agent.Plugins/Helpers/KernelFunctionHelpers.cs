// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Agent.Plugins.Helpers;

public class KernelFunctionHelpers
{
    public static TResult TryAction<TResult>(string className, Func<TResult> func, ILogger logger, [CallerMemberName] string caller = "Unknown")
    {
        try
        {
            logger.LogInformation($"[{className}] Performing action '{caller}'...");
            TResult res = func();
            logger.LogInformation($"[{className}] Completed action '{caller}'");
            return res;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error occurred while executing action");
            throw;
        }
    }
}

