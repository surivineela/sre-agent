// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System;
using Microsoft.Extensions.Logging;

namespace Agent.Tests.Common.XUnit
{
    internal interface ITestHostLoggerProvider : IDisposable
    {
        ITestHostLoggerProvider CreateChild(string name);

        ILogger<T> CreateLogger<T>();

        ILogger CreateLogger(string categoryName);
    }
}
