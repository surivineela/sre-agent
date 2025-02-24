// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Agent.Core.Extensions
{
    public static class ChatHistoryExtensions
    {
        const int MaxMessageLength = 1024;

        public static void AddSystemMessage(this ChatHistory history, ILogger logger, string message)
        {
            logger.LogInformation($"System > {Substring(message, MaxMessageLength)}");
            history.AddAssistantMessage(message);
        }

        public static void AddUserMessage(this ChatHistory history, ILogger logger, string message)
        {
            logger.LogInformation($"User > {Substring(message, MaxMessageLength)}");
            history.AddUserMessage(message);
        }

        public static void AddAssistantMessage(this ChatHistory history, ILogger logger, string message)
        {
            logger.LogInformation($"Assistant > {Substring(message, MaxMessageLength)}");
            history.AddAssistantMessage(message);
        }

        private static string Substring(string message, int length)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var line in message.Split(Environment.NewLine))
            {
                if (sb.Length + line.Length > length)
                {
                    sb.AppendLine("<message is truncated>");
                    break;
                }
                sb.AppendLine(line);
            }

            return sb.ToString();
        }
    }
}
