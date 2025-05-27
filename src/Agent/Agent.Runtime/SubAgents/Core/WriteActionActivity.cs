// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Attributes;
using Agent.Logging;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.SubAgents.Core;

[DurableTask]
public class WriteActionActivity : TaskActivity<WriteActionActivityInput, WriteActionActivityOutput>
{
    private readonly ILogger<WriteActionActivity> _logger;
    private readonly IToolsRepository _toolsRepository;
    private readonly ActionSettings _actionSettings;

    public WriteActionActivity(
        ILogger<WriteActionActivity> logger,
        IToolsRepository toolsRepository,
        ActionSettings actionSettings)
    {
        _logger = logger;
        _toolsRepository = toolsRepository;
        _actionSettings = actionSettings;
    }    public override Task<WriteActionActivityOutput> RunAsync(TaskActivityContext context, WriteActionActivityInput input)
    {
        try
        {
            if (input.FunctionCall == null)
            {
                return Task.FromResult(new WriteActionActivityOutput()
                {
                    IsWriteAction = false,
                });
            }

            var toolSignatures = input.ToolSignatures;
            var targetFunction = input.FunctionCall.Name;

            // Get all tools and find matching tool
            var aiFunctions = _toolsRepository.GetAllTools(toolSignatures).Select(_toolsRepository.FindAiFunction);
            var matchingTool = aiFunctions.SingleOrDefault(x => x.ToolFunction.Name == targetFunction);
            if (matchingTool == null)
            {
                _logger.LogInternalWarning("Could not find tool for function: {FunctionName}", targetFunction);
                return Task.FromResult(new WriteActionActivityOutput()
                {
                    IsWriteAction = false,
                });
            }

            //  if the function has the WriteAction attribute
            var writeActionAttribute = matchingTool.ToolFunction.UnderlyingMethod?.GetCustomAttribute<WriteActionAttribute>();
            if (writeActionAttribute == null)
            {
                return Task.FromResult(new WriteActionActivityOutput()
                {
                    IsWriteAction = false,
                });
            }

            _logger.LogInternalInformation("WriteActionActivity Found function with WriteAction attribute: {FunctionName} with {Arguments}", targetFunction, input.FunctionCall.Arguments);

            // Check if actionMode is ReadOnly
            if (_actionSettings.Mode == ActionMode.ReadOnly)
            {
                var prompt = $"You are in read-only mode. You should provide suggestions to user for what to do next. " +
                "Please format your suggestions in a user-friendly way:\n" +
                "- If suggesting CLI commands (like az cli, kubectl, docker, etc.), format them using ```shell code blocks for easy copying\n" +
                "- If the command is accurate and ready to use, tell the user they can copy and paste it directly\n" +
                "- Provide clear explanations of what each suggested action will do\n" +
                "- Use bullet points or numbered lists to organize multiple suggestions\n" +
                "- Always wait for user confirmation using AskUserForInput tool before proceeding\n" +
                "- Only proceed with next steps if user explicitly tells you the actions have been taken.";

                // Check if NeedExecute is true, and if so, add "agentmode" parameter to the function call
                if (writeActionAttribute.RunInReadOnlyMode)
                {
                    prompt += "\nThe suggestion will be the returned command from the next function call which runs in dry-run mode. " +
                             "Please format CLI commands using ```shell code blocks and let the user know if they can copy-paste directly.";
                    var modifiedArguments = new Dictionary<string, object?>(input.FunctionCall.Arguments ?? new Dictionary<string, object?>());
                    var modifiedFunctionCall = new FunctionCallContent(
                    input.FunctionCall.CallId,
                    input.FunctionCall.Name,
                    modifiedArguments);

                    _logger.LogInternalInformation("WriteActionActivity found WriteAction with NeedExecute=true, adding agentmode parameter");
                    modifiedArguments["agentmode"] = _actionSettings.Mode.ToString();
                    return Task.FromResult(new WriteActionActivityOutput()
                    {
                        IsWriteAction = true,
                        Prompt = prompt,
                        ModifiedFunctionCall = modifiedFunctionCall,
                    });
                }
                else
                {
                    prompt += $"\nThe suggestion is to call Function '{targetFunction}' with arguments: {System.Text.Json.JsonSerializer.Serialize(input.FunctionCall.Arguments)}. " +
                             "Please format this as a clear, actionable instruction for the user.";
                    return Task.FromResult(new WriteActionActivityOutput()
                    {
                        IsWriteAction = true,
                        Prompt = prompt,
                        NeedSkip = true,
                    });
                }

            }

            return Task.FromResult(new WriteActionActivityOutput()
            {
                IsWriteAction = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("Error WriteAction attribute: {Message}", ex.Message);
            return Task.FromResult(new WriteActionActivityOutput()
            {
                IsWriteAction = false,
            });
        }
    }
}
