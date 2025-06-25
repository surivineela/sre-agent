// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Agent.Tests.Common.XUnit
{
    internal sealed class XunitTestHostLogger<TCategory> : ILogger, ILogger<TCategory>
    {
        private readonly long _startTime;
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly string _loggingProviderName;
        private readonly string _categoryName;
        private readonly XunitLogFormatter _formatter;
        private readonly LogLevel _logLevel;

        public XunitTestHostLogger(long startTime, ITestOutputHelper testOutputHelper, IConfiguration loggingConfig, XunitLogFormatter formatter, string loggingProviderName, string categoryName)
        {
            _formatter = formatter;
            _testOutputHelper = testOutputHelper;
            _startTime = startTime;
            _loggingProviderName = loggingProviderName;
            _categoryName = categoryName.Split('.').Last();
           
            if (!Enum.TryParse<LogLevel>(loggingConfig["Logging:LogLevel:Agent.Tests.Integration.TestApplication"], out LogLevel logLevel))
            {
                if (!Enum.TryParse<LogLevel>(loggingConfig["Logging:LogLevel:Default"], out logLevel))
                {
                    logLevel = LogLevel.Debug;
                }
            }

            _logLevel = logLevel;
        }

        IDisposable ILogger.BeginScope<TState>(TState state) => default!;

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _logLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            try
            {
                _testOutputHelper.WriteLine(_formatter.FormatMessage(logLevel, eventId, state, exception, _startTime, "00000000000000000000000000000000", "0000000000000000", _loggingProviderName, _categoryName, formatter));
            }
            catch (InvalidOperationException)
            {
                // ignore exceptions when some thread tries to write a log after the test has terminated (i.e. background timer)
            }
        }
    }
}

