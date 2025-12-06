// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using System.Text.Json.Nodes;
using Agent.Cli.Helpers;
using Agent.Cli.Services;
using Agent.Common.Core.Manifests;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Cli.Commands;

public static class ScheduledTaskCommandHandlers
{
    public static async Task HandleCreateCommand(ParseResult parseResult)
    {
        var name = parseResult.GetValue(ScheduledTaskCommandOptions.Create.NameOption);
        var description = parseResult.GetValue(ScheduledTaskCommandOptions.Create.DescriptionOption);
        var cronExpression = parseResult.GetValue(ScheduledTaskCommandOptions.Create.CronExpressionOption);
        var agentPrompt = parseResult.GetValue(ScheduledTaskCommandOptions.Create.AgentPromptOption);
        var agent = parseResult.GetValue(ScheduledTaskCommandOptions.Create.AgentOption);
        var startTime = parseResult.GetValue(ScheduledTaskCommandOptions.Create.StartTimeOption);
        var endTime = parseResult.GetValue(ScheduledTaskCommandOptions.Create.EndTimeOption);
        var threadId = parseResult.GetValue(ScheduledTaskCommandOptions.Create.ThreadIdOption);
        var maxExecutions = parseResult.GetValue(ScheduledTaskCommandOptions.Create.MaxExecutionsOption);
        var notificationChannel = parseResult.GetValue(ScheduledTaskCommandOptions.Create.NotificationChannelOption);

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(cronExpression) || string.IsNullOrWhiteSpace(agentPrompt))
        {
            ConsoleUI.WriteStatus(false, "Name, cron expression, and agent prompt are required.");
            Environment.Exit(1);
            return;
        }

        try
        {
            using var apiService = new ApiService();

            ConsoleUI.WriteSection($"Creating scheduled task '{name}'");

            // Create the task JSON object
            var task = new JsonObject
            {
                ["name"] = name,
                ["description"] = description ?? string.Empty,
                ["cronExpression"] = cronExpression,
                ["agentPrompt"] = agentPrompt,
                ["agent"] = agent,
                ["startTime"] = startTime,
                ["endTime"] = endTime,
                ["threadId"] = threadId,
                ["maxExecutions"] = maxExecutions > 0 ? maxExecutions : null,
                ["notificationChannel"] = notificationChannel
            };

            // Save YAML locally first
            try
            {
                var manifest = new ScheduledTaskManifest
                {
                    ApiVersion = "azuresre.ai/v1",
                    Kind = "ScheduledTask",
                    Metadata = new ManifestMetadata { Name = name },
                    Spec = new ScheduledTaskSpec
                    {
                        Name = name,
                        Description = description ?? string.Empty,
                        Cron = cronExpression,
                        AgentPrompt = agentPrompt,
                        Agent = agent,
                        StartTime = startTime != null ? DateTime.Parse(startTime) : DateTime.UtcNow,
                        EndTime = endTime != null ? DateTime.Parse(endTime) : null,
                        ThreadId = threadId,
                        MaxExecutions = maxExecutions > 0 ? maxExecutions : null,
                        NotificationChannel = notificationChannel
                    }
                };

                var ser = new SerializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .DisableAliases()
                    .Build();
                var yaml = ser.Serialize(manifest);

                var dir = Path.Combine("scheduledtasks", name);
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"{name}.yaml");
                await File.WriteAllTextAsync(path, yaml, System.Text.Encoding.UTF8);

                ConsoleUI.WriteBullet($"Saved YAML manifest to: {path}", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteStatus(false, $"Warning: Failed to save YAML locally: {ex.Message}");
                // Continue with API creation even if local save fails
            }

            // Create the task
            ConsoleUI.WriteBullet("Creating scheduled task...", ConsoleColor.Cyan);
            var (createSuccess, createMessage) = await apiService.CreateScheduledTaskAsync(task);
            if (!createSuccess)
            {
                ConsoleUI.WriteStatus(false, $"Failed to create scheduled task: {createMessage}");
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteStatus(true, "Successfully created scheduled task.");
            ConsoleUI.WriteKeyValue("Name", name);
            ConsoleUI.WriteKeyValue("Cron Expression", cronExpression);
            if (!string.IsNullOrWhiteSpace(description))
                ConsoleUI.WriteKeyValue("Description", description);
            if (!string.IsNullOrWhiteSpace(agent))
                ConsoleUI.WriteKeyValue("Agent", agent);
            if (maxExecutions > 0)
                ConsoleUI.WriteKeyValue("Max Executions", maxExecutions.ToString());

            ConsoleUI.WriteInfo("The task is now scheduled and will execute according to the specified cron expression.");
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Error creating scheduled task: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task HandleListCommand(ParseResult parseResult)
    {
        var verbose = parseResult.GetValue(ScheduledTaskCommandOptions.List.VerboseOption);
        var filterThreadId = parseResult.GetValue(ScheduledTaskCommandOptions.List.FilterThreadIdOption);
        var filterStatus = parseResult.GetValue(ScheduledTaskCommandOptions.List.FilterStatusOption);

        try
        {
            using var apiService = new ApiService();

            ConsoleUI.WriteBullet("Fetching scheduled tasks...", ConsoleColor.Cyan);

            // Fetch scheduled tasks
            var tasks = await apiService.GetScheduledTasksAsync();
            if (tasks == null)
            {
                ConsoleUI.WriteStatus(false, "Failed to fetch scheduled tasks.");
                Environment.Exit(1);
                return;
            }

            // Apply filters
            var filteredTasks = tasks.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(filterThreadId))
            {
                filteredTasks = filteredTasks.Where(task =>
                    task["threadId"]?.ToString() == filterThreadId);
            }

            if (!string.IsNullOrWhiteSpace(filterStatus))
            {
                filteredTasks = filteredTasks.Where(task =>
                    task["status"]?.ToString()?.Equals(filterStatus, StringComparison.OrdinalIgnoreCase) == true);
            }

            var taskList = filteredTasks.ToList();

            if (taskList.Count == 0)
            {
                ConsoleUI.WriteInfo("No scheduled tasks found matching the criteria.");
                return;
            }

            ConsoleUI.WriteSection($"Found {taskList.Count} scheduled task(s):");

            // Display tasks
            for (int i = 0; i < taskList.Count; i++)
            {
                var task = taskList[i];
                var taskId = task["id"]?.ToString() ?? "N/A";
                var taskName = task["name"]?.ToString() ?? "Unknown";
                var status = task["status"]?.ToString() ?? "N/A";
                var cronExpression = task["cronExpression"]?.ToString() ?? "N/A";
                var executionCount = task["executionCount"]?.ToString() ?? "0";
                var lastExecution = task["lastExecutionTime"]?.ToString();
                var nextExecution = task["nextExecutionTime"]?.ToString();

                Console.WriteLine($"[{i + 1}] {taskName}");
                ConsoleUI.WriteKeyValue("ID", taskId, 4);
                ConsoleUI.WriteKeyValue("Status", status, 4);
                ConsoleUI.WriteKeyValue("Schedule", cronExpression, 4);
                ConsoleUI.WriteKeyValue("Executions", executionCount, 4);

                if (verbose)
                {
                    var description = task["description"]?.ToString();
                    var threadId = task["threadId"]?.ToString();
                    var maxExecutions = task["maxExecutions"]?.ToString();
                    var createdBy = task["createdBy"]?.ToString();
                    var createdAt = task["createdAt"]?.ToString();

                    if (!string.IsNullOrEmpty(description))
                        ConsoleUI.WriteKeyValue("Description", description, 4);

                    if (!string.IsNullOrEmpty(threadId))
                        ConsoleUI.WriteKeyValue("Thread ID", threadId, 4);

                    if (!string.IsNullOrEmpty(maxExecutions))
                        ConsoleUI.WriteKeyValue("Max Executions", maxExecutions, 4);

                    if (!string.IsNullOrEmpty(lastExecution))
                        ConsoleUI.WriteKeyValue("Last Execution", FormatDateTime(lastExecution), 4);

                    if (!string.IsNullOrEmpty(nextExecution))
                        ConsoleUI.WriteKeyValue("Next Execution", FormatDateTime(nextExecution), 4);

                    if (!string.IsNullOrEmpty(createdBy))
                        ConsoleUI.WriteKeyValue("Created By", createdBy, 4);

                    if (!string.IsNullOrEmpty(createdAt))
                        ConsoleUI.WriteKeyValue("Created At", FormatDateTime(createdAt), 4);
                }

                if (i < taskList.Count - 1)
                {
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Error listing scheduled tasks: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task HandleGetCommand(ParseResult parseResult)
    {
        var taskId = parseResult.GetValue(ScheduledTaskCommandOptions.Get.TaskIdOption);

        if (string.IsNullOrWhiteSpace(taskId))
        {
            ConsoleUI.WriteStatus(false, "Task ID is required.");
            Environment.Exit(1);
            return;
        }

        try
        {
            using var apiService = new ApiService();

            ConsoleUI.WriteBullet($"Fetching scheduled task '{taskId}'...", ConsoleColor.Cyan);

            var task = await apiService.GetScheduledTaskAsync(taskId);
            if (task == null)
            {
                ConsoleUI.WriteStatus(false, $"Scheduled task '{taskId}' not found.");
                Environment.Exit(1);
                return;
            }

            var taskName = task["name"]?.ToString() ?? "Unknown";
            var status = task["status"]?.ToString() ?? "N/A";
            var description = task["description"]?.ToString();
            var cronExpression = task["cronExpression"]?.ToString() ?? "N/A";
            var agentPrompt = task["agentPrompt"]?.ToString();
            var executionCount = task["executionCount"]?.ToString() ?? "0";
            var maxExecutions = task["maxExecutions"]?.ToString();
            var lastExecution = task["lastExecutionTime"]?.ToString();
            var nextExecution = task["nextExecutionTime"]?.ToString();
            var threadId = task["threadId"]?.ToString();
            var createdBy = task["createdBy"]?.ToString();
            var createdAt = task["createdAt"]?.ToString();

            ConsoleUI.WriteSection($"Scheduled Task: {taskName}");
            ConsoleUI.WriteKeyValue("ID", taskId);
            ConsoleUI.WriteKeyValue("Status", status);
            ConsoleUI.WriteKeyValue("Schedule", cronExpression);

            if (!string.IsNullOrEmpty(description))
                ConsoleUI.WriteKeyValue("Description", description);

            if (!string.IsNullOrEmpty(agentPrompt))
            {
                ConsoleUI.WriteKeyValue("Agent Prompt", agentPrompt.Length > 100 ?
                    agentPrompt.Substring(0, 100) + "..." : agentPrompt);
            }

            ConsoleUI.WriteKeyValue("Executions", maxExecutions != null ?
                $"{executionCount}/{maxExecutions}" : executionCount);

            if (!string.IsNullOrEmpty(lastExecution))
                ConsoleUI.WriteKeyValue("Last Execution", FormatDateTime(lastExecution));

            if (!string.IsNullOrEmpty(nextExecution))
                ConsoleUI.WriteKeyValue("Next Execution", FormatDateTime(nextExecution));

            if (!string.IsNullOrEmpty(threadId))
                ConsoleUI.WriteKeyValue("Thread ID", threadId);

            if (!string.IsNullOrEmpty(createdBy))
                ConsoleUI.WriteKeyValue("Created By", createdBy);

            if (!string.IsNullOrEmpty(createdAt))
                ConsoleUI.WriteKeyValue("Created At", FormatDateTime(createdAt));
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Error fetching scheduled task: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task HandlePauseCommand(ParseResult parseResult)
    {
        var taskId = parseResult.GetValue(ScheduledTaskCommandOptions.Pause.TaskIdOption);

        if (string.IsNullOrWhiteSpace(taskId))
        {
            ConsoleUI.WriteStatus(false, "Task ID is required.");
            Environment.Exit(1);
            return;
        }

        try
        {
            using var apiService = new ApiService();

            ConsoleUI.WriteBullet($"Pausing scheduled task '{taskId}'...", ConsoleColor.Cyan);

            var success = await apiService.PauseScheduledTaskAsync(taskId);
            if (!success)
            {
                ConsoleUI.WriteStatus(false, $"Failed to pause scheduled task '{taskId}'. Task may not exist or already be paused.");
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteStatus(true, $"Successfully paused scheduled task '{taskId}'.");
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Error pausing scheduled task: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task HandleResumeCommand(ParseResult parseResult)
    {
        var taskId = parseResult.GetValue(ScheduledTaskCommandOptions.Resume.TaskIdOption);

        if (string.IsNullOrWhiteSpace(taskId))
        {
            ConsoleUI.WriteStatus(false, "Task ID is required.");
            Environment.Exit(1);
            return;
        }

        try
        {
            using var apiService = new ApiService();

            ConsoleUI.WriteBullet($"Resuming scheduled task '{taskId}'...", ConsoleColor.Cyan);

            var success = await apiService.ResumeScheduledTaskAsync(taskId);
            if (!success)
            {
                ConsoleUI.WriteStatus(false, $"Failed to resume scheduled task '{taskId}'. Task may not exist or already be active.");
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteStatus(true, $"Successfully resumed scheduled task '{taskId}'.");
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Error resuming scheduled task: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task HandleDeleteCommand(ParseResult parseResult)
    {
        var taskId = parseResult.GetValue(ScheduledTaskCommandOptions.Delete.TaskIdOption);

        if (string.IsNullOrWhiteSpace(taskId))
        {
            ConsoleUI.WriteStatus(false, "Task ID is required.");
            Environment.Exit(1);
            return;
        }

        try
        {
            using var apiService = new ApiService();

            ConsoleUI.WriteBullet($"Deleting scheduled task '{taskId}'...", ConsoleColor.Cyan);

            var success = await apiService.DeleteScheduledTaskAsync(taskId);
            if (!success)
            {
                ConsoleUI.WriteStatus(false, $"Failed to delete scheduled task '{taskId}'. Task may not exist.");
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteStatus(true, $"Successfully deleted scheduled task '{taskId}'.");
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Error deleting scheduled task: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task HandleApplyYamlCommand(ParseResult parseResult)
    {
        // Apply a ScheduledTask YAML manifest (apiVersion/kind/spec)
        var filePath = parseResult.GetValue(ScheduledTaskCommandOptions.Apply.FileOption);
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            ConsoleUI.WriteStatus(false, string.IsNullOrWhiteSpace(filePath) ? "--file is required" : $"File not found: {filePath}");
            Environment.Exit(1);
            return;
        }

        try
        {
            var yaml = await File.ReadAllTextAsync(filePath);
            var des = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            var manifest = des.Deserialize<ScheduledTaskManifest>(yaml);
            if (!string.Equals(manifest?.Kind, "ScheduledTask", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleUI.WriteStatus(false, "YAML kind must be ScheduledTask");
                Environment.Exit(1);
                return;
            }

            // Build CreateScheduledTaskRequest JSON from manifest.Spec
            var spec = manifest!.Spec;
            var start = spec.StartTime ?? DateTime.UtcNow;
            var end = spec.EndTime;
            if (end == null && spec.DurationHours is int dh && dh > 0)
            {
                end = start.AddHours(dh);
            }

            var task = new JsonObject
            {
                ["name"] = spec.Name,
                ["description"] = spec.Description ?? string.Empty,
                ["cronExpression"] = spec.ResolveCronExpression(),
                ["agentPrompt"] = spec.AgentPrompt,
                ["agent"] = spec.Agent,
                ["startTime"] = start.ToUniversalTime().ToString("o"),
                ["endTime"] = end?.ToUniversalTime().ToString("o"),
                ["threadId"] = spec.ThreadId,
                ["maxExecutions"] = spec.MaxExecutions,
                ["notificationChannel"] = spec.NotificationChannel
            };

            using var apiService = new ApiService();
            ConsoleUI.WriteBullet("Applying scheduled task manifest...", ConsoleColor.Cyan);
            var (ok, msg) = await apiService.CreateScheduledTaskAsync(task);
            ConsoleUI.WriteStatus(ok, ok ? "Scheduled task created" : msg);
            Environment.Exit(ok ? 0 : 1);
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Failed to apply scheduled task YAML: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task HandleQuickstartCommand(ParseResult parseResult)
    {
        // Interactive hello world scheduled task
        var name = parseResult.GetValue(ScheduledTaskCommandOptions.Quickstart.NameOption) ?? "hello-world-check";
        var cron = parseResult.GetValue(ScheduledTaskCommandOptions.Quickstart.CronOption) ?? "*/15 * * * *";
        var dur = parseResult.GetValue(ScheduledTaskCommandOptions.Quickstart.DurationHoursOption);
        var agent = parseResult.GetValue(ScheduledTaskCommandOptions.Quickstart.AgentOption);
        var apply = parseResult.GetValue(ScheduledTaskCommandOptions.Quickstart.ApplyOption);

        if (dur <= 0) dur = 1;

        ConsoleUI.WriteSection("Hello World Scheduled Task");
        ConsoleUI.WriteKeyValue("Name", name);
        ConsoleUI.WriteKeyValue("Cron", cron);
        ConsoleUI.WriteKeyValue("Duration (hours)", dur.ToString());
        if (!string.IsNullOrWhiteSpace(agent)) ConsoleUI.WriteKeyValue("Agent", agent);

        var manifest = new ScheduledTaskManifest
        {
            ApiVersion = "azuresre.ai/v1",
            Kind = "ScheduledTask",
            Metadata = new ManifestMetadata { Name = name },
            Spec = new ScheduledTaskSpec
            {
                Name = name,
                Cron = cron,
                AgentPrompt = "Say: Hello world! The current UTC time is {{now}}.",
                StartTime = DateTime.UtcNow,
                DurationHours = dur,
                ThreadId = null,
                MaxExecutions = null,
                Description = "Hello world recurring check from CLI quickstart"
            }
        };

        var ser = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .DisableAliases()
            .Build();
        var yaml = ser.Serialize(manifest);

        var dir = Path.Combine("scheduledtasks", name);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{name}.yaml");
        await File.WriteAllTextAsync(path, yaml, System.Text.Encoding.UTF8);

        ConsoleUI.WriteStatus(true, "Manifest generated");
        ConsoleUI.WriteKeyValue("Path", path);

        if (apply)
        {
            // Apply by calling the API directly using the same mapping as HandleApplyYamlCommand
            var spec = manifest.Spec;
            var start = spec.StartTime ?? DateTime.UtcNow;
            var end = spec.EndTime;
            if (end == null && spec.DurationHours is int dh2 && dh2 > 0)
            {
                end = start.AddHours(dh2);
            }
            var task = new JsonObject
            {
                ["name"] = spec.Name,
                ["description"] = spec.Description ?? string.Empty,
                ["cronExpression"] = spec.ResolveCronExpression(),
                ["agentPrompt"] = spec.AgentPrompt,
                ["agent"] = spec.Agent,
                ["startTime"] = start.ToUniversalTime().ToString("o"),
                ["endTime"] = end?.ToUniversalTime().ToString("o"),
                ["threadId"] = spec.ThreadId,
                ["maxExecutions"] = spec.MaxExecutions,
                ["notificationChannel"] = spec.NotificationChannel
            };
            using var api = new ApiService();
            var (ok, msg) = await api.CreateScheduledTaskAsync(task);
            ConsoleUI.WriteStatus(ok, ok ? $"Applied: {name}" : msg);
            Environment.Exit(ok ? 0 : 1);
            return;
        }
        else
        {
            ConsoleUI.WriteInfo("Use 'srectl scheduledtask apply --file " + path + "' to create it on the server.");
        }
    }

    private static string FormatDateTime(string dateTimeString)
    {
        if (DateTime.TryParse(dateTimeString, out var dateTime))
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss UTC");
        }
        return dateTimeString;
    }
}
