// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;

namespace Agent.Plugins;

public class DGrepException : Exception
{
    public DGrepException(string message) : base(message)
    {
    }

    public DGrepException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public DGrepException() : base("An error occurred in the DGrep plugin.")
    {
    }
}
