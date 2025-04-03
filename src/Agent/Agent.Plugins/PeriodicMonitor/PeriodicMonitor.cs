// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Core;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using System.Collections.Concurrent;
using Agent.Plugins.Models;

namespace Agent.Plugins.PeriodicMonitor
{
    public class PeriodicMonitor : IPeriodicMonitor
    {
        private static readonly ConcurrentDictionary<string, PeriodicMonitorContext> _resources = new();
        private static readonly Task _scheduler = StartScheduler();

        private static async Task StartScheduler()
        {
            while (true)
            {
                foreach (var ctx in _resources.Values)
                {
                    lock (_resources)
                    {
                        if (!ctx.Task.IsCompleted
                            || (ctx.LastExecution ?? DateTime.MinValue) + ctx.MonitorInterval > DateTime.Now)
                        {
                            continue;
                        }

                        _resources[ctx.ResourceId] = ctx with
                        {
                            Task = ExecuteMonitorOperation(ctx),
                            LastExecution = DateTime.Now,
                        };
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        private static async Task ExecuteMonitorOperation(PeriodicMonitorContext ctx)
        {
            ctx.ChatHistory.AddSystemMessage($"The time now is {DateTime.Now}. Please start health inspection for this app, " +
                $"including metrics and security hole analysis, do not call diagnose_appservice function. " +
                $"Append <unhealthy> to the end of message if the overall condition of this app is unhealthy or has any security hole detected");

            var chatCompletionService = ctx.kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatCompletionService.GetChatMessageContentAsync(
                ctx.ChatHistory,
                executionSettings: new()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                },
                kernel: ctx.kernel);
            var resultContent = result.Content ?? string.Empty;
            ctx.ChatHistory.AddAssistantMessage(resultContent);

            var unhealthy = resultContent.EndsWith("<unhealthy>");

            lock (_resources)
            {
                _resources[ctx.ResourceId] = _resources[ctx.ResourceId] with
                {
                    LastCheckWasHealthy = !unhealthy,
                };
            }

            if (unhealthy && ctx.LastCheckWasHealthy != false)
            {
                // Msg to Teams
                var msgWithoutTag = resultContent.Substring(startIndex: 0, length: resultContent.Length - "<unhealthy>".Length);
                await GlobalStatic.TeamsConnector.PostMessageAsync(new TeamsMessage("Resource monitor found an issue with your app!"));
                await GlobalStatic.TeamsConnector.PostMessageAsync(new TeamsMessage(msgWithoutTag));

                // Push back the conclusion to the main history
                await ChatHistoryPersistency.ChatHistoryTransition(
                    history =>
                    {
                        history.AddAssistantMessage(msgWithoutTag);
                        return Task.FromResult(0);
                    });
            }
            else if (!unhealthy && ctx.LastCheckWasHealthy == false)
            {
                await GlobalStatic.TeamsConnector.PostMessageAsync(new TeamsMessage("Your app is now healthy!"));
                await GlobalStatic.TeamsConnector.PostMessageAsync(new TeamsMessage(resultContent));
                // Push back the conclusion to the main history
                await ChatHistoryPersistency.ChatHistoryTransition(
                    history =>
                    {
                        history.AddAssistantMessage(resultContent);
                        return Task.FromResult(0);
                    });
            }
        }

        public PeriodicMonitorInfo? Get(string resourceId)
        {
            return _resources.GetValueOrDefault(resourceId)?.Info;
        }

        public async Task<string?> Summarize(Kernel kernel, string resourceId, string userPrompt)
        {
            if (!_resources.TryGetValue(resourceId, out var ctx))
            {
                return null;
            }

            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
            ctx.ChatHistory.AddUserMessage(userPrompt);

            try
            {
                var result = await chatCompletionService.GetChatMessageContentAsync(
                    ctx.ChatHistory,
                    executionSettings: new()
                    {
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                    },
                    kernel: kernel);
                ctx.ChatHistory.AddAssistantMessage(result.Content ?? string.Empty);
                return result.Content ?? string.Empty;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public bool Start(Kernel kernel, string resourceId, TimeSpan interval, out PeriodicMonitorInfo info)
        {
            lock (_resources)
            {
                if (_resources.TryGetValue(resourceId, out var ctx))
                {
                    info = ctx.Info;
                    return false;
                }

                info = (_resources[resourceId] = new PeriodicMonitorContext(
                    kernel,
                    resourceId,
                    interval,
                    Task: Task.CompletedTask,
                    LastCheckWasHealthy: null,
                    LastExecution: null,
                    ChatHistory: CreateNewChatHistory(resourceId)))
                    .Info;
                return true;
            }
        }

        private static ChatHistory CreateNewChatHistory(string resourceId)
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage($"You are a periodic app service resource health status monitor for this resource: {resourceId} , you execute at a predefined interval to find" +
                $" live issue of the app as well as detect security holes. Make sure you DO NOT call diagnose_appservice function. You should get straight" +
                $" to the metric and security related plugins and analyze their output. In every single response, you should prefix the resource id to the top" +
                $" of the output. You should also append <unhealthy>, (angle bracket included), to the end of message if the overall condition of this app is " +
                $"unhealthy or has any security hole detected");
            return chatHistory;
        }

        public PeriodicMonitorInfo? UpdateFrequency(string resourceId, TimeSpan interval)
        {
            lock (_resources)
            {
                if (!_resources.TryGetValue(resourceId, out var ctx))
                {
                    return null;
                }

                var newCtx = _resources[resourceId] = ctx with
                {
                    MonitorInterval = interval
                };

                return newCtx.Info;
            }
        }

        private sealed record PeriodicMonitorContext(
            Kernel kernel,
            string ResourceId,
            TimeSpan MonitorInterval,
            Task Task,
            bool? LastCheckWasHealthy,
            DateTime? LastExecution,
            ChatHistory ChatHistory)
        {
            public PeriodicMonitorInfo Info => new PeriodicMonitorInfo(
                ResourceId,
                MonitorInterval,
                LastCheckWasHealthy,
                LastExecution);
        }
    }
}

