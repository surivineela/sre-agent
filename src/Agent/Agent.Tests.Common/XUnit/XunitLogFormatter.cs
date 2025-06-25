// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Agent.Tests.Common.XUnit
{
    /// <summary>
    /// Formats log messages for easy viewing in XUnit output.
    /// </summary>
    internal sealed class XunitLogFormatter
    { 
        public string FormatMessage<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, long startTime, string traceId, string spanId, string logProviderName, string categoryName, Func<TState, Exception?, string> formatter)
        {
            var messageBuilder = new StringBuilder();

            var levelString = logLevel switch
            {
                LogLevel.Critical => "CRIT ",
                LogLevel.Warning => "WARN ",
                LogLevel.Error => "ERROR",
                LogLevel.Information => "INFO ",
                LogLevel.Debug => "DEBUG",
                _ => "UNKNOWN",
            };

            var elapsedTime = Stopwatch.GetElapsedTime(startTime);

            if (categoryName.Contains('.'))
            {
                string[] categoryParts = categoryName.Split('.', StringSplitOptions.RemoveEmptyEntries);

                if (categoryParts.Length > 1)
                {
                    categoryName = categoryParts[categoryParts.Length - 2] + "." + categoryParts[categoryParts.Length - 1];
                }
            }

            categoryName = categoryName.Length > 35 ? categoryName[..32] + ".." : categoryName;
            logProviderName = logProviderName.Length > 35 ? logProviderName[..32] + ".." : logProviderName;

            messageBuilder.Append(CultureInfo.InvariantCulture, $"[{elapsedTime:mm\\:ss\\.fff}] {levelString}");
            messageBuilder.Append($" {logProviderName}".PadRight(35));
            messageBuilder.Append($" {categoryName}".PadRight(35));
            messageBuilder.Append($"[EventID]: ");
            messageBuilder.Append(eventId.Id.ToString().PadRight(5));
            messageBuilder.Append(CultureInfo.InvariantCulture, $" [traceID]: {traceId} [spanID]: {spanId}");

            var formattedMessage = formatter(state, exception);
            if (!string.IsNullOrEmpty(formattedMessage))
            {
                messageBuilder
                    .Append(' ')
                    .Append(formattedMessage);
            }

            while (messageBuilder.Length > 0 && (char.IsWhiteSpace(messageBuilder[^1]) || messageBuilder[^1] == ','))
            {
                messageBuilder.Length--;
            }

            return messageBuilder.ToString();
        }
    }
}

