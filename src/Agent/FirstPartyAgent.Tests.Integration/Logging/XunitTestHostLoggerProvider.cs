// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace FirstPartyAgent.Tests.Integration.Logging
{
    internal class XunitTestHostLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _output;
        public XunitTestHostLoggerProvider(ITestOutputHelper output)
        {
            _output = output;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new XunitTestHostLogger(_output, categoryName);
        }

        public void Dispose()
        {
        }
    }
}
