// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Agent.Tests.Common.XUnit
{
    internal sealed class XunitTestHostLoggerProvider : ITestHostLoggerProvider, ILoggerProvider
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly long _startTime;
        private readonly string _loggingProviderName;
        private readonly IConfiguration _configuration;

        public XunitTestHostLoggerProvider(ITestOutputHelper testOutputHelper, IConfiguration loggingConfig, long startTime, string loggingProviderName)
        {
            _testOutputHelper = testOutputHelper;
            _configuration = loggingConfig;
            _loggingProviderName = loggingProviderName;
            _startTime = startTime;
        }

        public ITestHostLoggerProvider CreateChild(string loggingProviderName)
        {
            return new XunitTestHostLoggerProvider(_testOutputHelper, _configuration, _startTime, $"{_loggingProviderName} - {loggingProviderName}");
        }

        public ILogger<T> CreateLogger<T>()
        {
            return new XunitTestHostLogger<T>(
                _startTime,
                _testOutputHelper,
                _configuration,
                new XunitLogFormatter(),
                _loggingProviderName,
                typeof(T).FormatName());
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new XunitTestHostLogger<object>(
                _startTime,
                _testOutputHelper,
                _configuration,
                new XunitLogFormatter(),
                _loggingProviderName,
                categoryName);
        }

        public void Dispose()
        {
        }
    }
}
