// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Agent.Core.Helpers;
using DurableTask.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Cmd
{
    /// <summary>
    /// Allows running agent scenarios in bulk by calling the APIs to create a new thread and then monitoring it until it reaches a terminal state.
    /// </summary>
    public class ScenarioCommand
    {
        private readonly ILogger<ScenarioCommand> _logger;
        private readonly HttpClient _httpClient;
        private readonly IChatClient _chatClient;
        private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        private string _exportDir;

        public ScenarioCommand(
            ILogger<ScenarioCommand> logger,
            HttpClient httpClient,
            [FromKeyedServices("function-invocation-enabled")] IChatClient chatClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            _chatClient = chatClient;
        }

        public void RunScenario(CommandLineApplication command)
        {
            command.Description = "Run agent scenarios with batching";
            command.HelpOption("-?|-h|--help");
            
            var countArg = command.Argument("count", "Number of scenario runs");
            var messageArg = command.Argument("message", "Start message for the scenario");
            var baseUrlOption = command.Option("-u|--url", "Base URL (default: http://localhost:5073)", CommandOptionType.SingleValue);

            command.OnExecute(async () =>
            {
                if (string.IsNullOrEmpty(countArg.Value) || !int.TryParse(countArg.Value, out int count) || count <= 0)
                {
                    Console.WriteLine("Error: Count must be a positive integer.");
                    return 1;
                }

                if (string.IsNullOrEmpty(messageArg.Value))
                {
                    Console.WriteLine("Error: Start message must be provided.");
                    return 1;
                }

                var baseUrl = baseUrlOption.HasValue() ? baseUrlOption.Value() : "http://localhost:5073";
                
                await RunScenariosAsync(count, messageArg.Value, baseUrl);
                return 0;
            });
        }

        private async Task RunScenariosAsync(int totalCount, string startMessage, string baseUrl)
        {
            const int batchSize = 5; // Reasonable batch size for concurrency
            var batches = (int)Math.Ceiling((double)totalCount / batchSize);
            
            _logger.LogInformation($"Starting {totalCount} scenario runs in {batches} batches of up to {batchSize} concurrent runs each");
            Console.WriteLine($"Starting {totalCount} scenario runs in {batches} batches of up to {batchSize} concurrent runs each");

            var completedRuns = 0;
            var allThreadIds = new List<string>();

            _exportDir = GetExportDirectory();
            Directory.CreateDirectory(_exportDir);

            for (int batchIndex = 0; batchIndex < batches; batchIndex++)
            {
                var runsInThisBatch = Math.Min(batchSize, totalCount - (batchIndex * batchSize));
                Console.WriteLine($"Starting batch {batchIndex + 1}/{batches} with {runsInThisBatch} runs...");                // Start all runs in this batch concurrently
                var batchTasks = new List<Task<string?>>();
                for (int i = 0; i < runsInThisBatch; i++)
                {
                    batchTasks.Add(StartScenarioRunAsync(startMessage, baseUrl));

                    // stagger
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                // Wait for all runs in the batch to start and get their thread IDs
                var batchThreadIds = await Task.WhenAll(batchTasks);
                allThreadIds.AddRange(batchThreadIds.Where(id => !string.IsNullOrEmpty(id))!);

                Console.WriteLine($"Batch {batchIndex + 1} started with {batchThreadIds.Length} runs");                // Monitor all runs in this batch until completion
                var monitoringTasks = batchThreadIds
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Select(threadId => MonitorAndSaveScenarioAsync(threadId!, baseUrl))
                    .ToList();

                await Task.WhenAll(monitoringTasks);
                
                completedRuns += runsInThisBatch;
                Console.WriteLine($"Batch {batchIndex + 1} completed. Total progress: {completedRuns}/{totalCount}");
            }

            Console.WriteLine($"All {totalCount} scenario runs completed successfully!");
            Console.WriteLine($"Results saved to: {GetExportDirectory()}");
        }

        private async Task<string?> StartScenarioRunAsync(string startMessage, string baseUrl)
        {
            try
            {
                var requestBody = new
                {
                    startMessage = new
                    {
                        text = startMessage,
                        userId = "web-client-user",
                        displayName = "Web Client User"
                    }
                };

                var json = JsonSerializer.Serialize(requestBody, _serializerOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{baseUrl}/api/v1/threads", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to start scenario run: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson, _serializerOptions);
                
                if (responseObj.TryGetProperty("id", out var idProperty))
                {
                    var threadId = idProperty.GetString();
                    _logger.LogInformation($"Started scenario run with thread ID: {threadId}");
                    return threadId;
                }

                _logger.LogError("Response did not contain an 'id' property");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting scenario run");
                return null;
            }
        }

        private async Task MonitorAndSaveScenarioAsync(string threadId, string baseUrl)
        {
            try
            {
                var autoReplyHelper = new AutoReplyHelper(_chatClient);
                var tokenSource = new CancellationTokenSource();
                if(!Debugger.IsAttached)
                {
                    tokenSource.CancelAfter(TimeSpan.FromMinutes(5));
                } 
                _logger.LogInformation($"Monitoring thread {threadId}...");

                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), tokenSource.Token); 

                    var chatHistoryResponse = await _httpClient.GetAsync($"{baseUrl}/api/v1/chathistory/agentFramework/{threadId}");
                    
                    if (!chatHistoryResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning($"Failed to get chat history for thread {threadId}: {chatHistoryResponse.StatusCode}");
                        continue;
                    }

                    var responseJson = await chatHistoryResponse.Content.ReadAsStringAsync();
                    
                    // Save the thread file on every update
                    await SaveThreadResultAsync(threadId, responseJson);
                    
                    var messages = JsonSerializer.Deserialize<List<ChatMessage>>(responseJson, _serializerOptions);
                    var reply = await autoReplyHelper.GetReply(messages);

                    if(reply != null)
                    {
                        var requestBody = new
                        {
                            text = reply,
                            userId = "web-client-user",
                            displayName = "Web Client User"
                        };

                        var postReplyResponse = await _httpClient.PostAsync($"{baseUrl}/api/v1/threads/{threadId}/messages", JsonContent.Create(requestBody));
                        if(!postReplyResponse.IsSuccessStatusCode)
                        {
                            _logger.LogError($"Failed to post reply for thread {threadId}: {postReplyResponse.StatusCode} - {await postReplyResponse.Content.ReadAsStringAsync()}");
                        }
                        else
                        {
                            _logger.LogInformation($"Posted auto-reply for thread {threadId}: {reply}");
                        }
                    }

                    if(autoReplyHelper.AssessedState == AutoReplyHelper.AssessedAgentState.Findings)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error monitoring thread {threadId}");
            }
        }

        private async Task SaveThreadResultAsync(string threadId, string jsonContent)
        {
            try
            {
                var messages = JsonSerializer.Deserialize<ChatMessage[]>(jsonContent, _serializerOptions);
                jsonContent = JsonSerializer.Serialize(messages, _serializerOptions);
                
                var filePath = Path.Combine(_exportDir, $"{threadId}.json");
                await File.WriteAllTextAsync(filePath, jsonContent);
                
                _logger.LogInformation($"Saved thread {threadId} result to {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving thread {threadId} result");
            }
        }

        private static string GetExportDirectory()
        {
            // Get the directory where the Agent.Cmd.exe is located
            var executableDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
            
            // Create timestamp with minute-level granularity in format yyyy-MM-dd_HH-mm
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
            
            // Navigate up to find the src directory, then go to Agent.Evals\ToolReplayLogs\Export
            var srcDir = executableDir;
            while (!string.IsNullOrEmpty(srcDir) && !Directory.Exists(Path.Combine(srcDir, "Agent")))
            {
                srcDir = Directory.GetParent(srcDir)?.FullName;
            }
            
            if (!string.IsNullOrEmpty(srcDir))
            {
                return Path.Combine(srcDir, "Agent", "Agent.Evals", "ToolReplayLogs", "Export", timestamp);
            }
            
            // Fallback to current directory if we can't find the proper structure
            return Path.Combine(Directory.GetCurrentDirectory(), "Export", timestamp);
        }
    }
}
