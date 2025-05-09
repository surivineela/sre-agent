// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Logging;
using Agent.Plugins;
using Agent.Runtime.MetaAgent;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.Core;


public sealed record ChartToolCallInput(
    FunctionCallContent FunctionCallContent,
    Guid? ThreadId
);

[DurableTask]
public class ChartToolCallActivity : TaskActivity<ChartToolCallInput, ExecuteActionOutput>
{
    private readonly IChartPlugin _chartPlugin;    
    private readonly ILogger<GenericExecuteActionActivity> _logger;

    public ChartToolCallActivity(
        IChartPlugin chartPlugin,
        IToolsRepository toolsRepository,
        ILogger<GenericExecuteActionActivity> logger
        )
    {
        _chartPlugin = chartPlugin;
        _logger = logger;
    }

    public async override Task<ExecuteActionOutput> RunAsync(
        TaskActivityContext context,
        ChartToolCallInput input)
    {
        _chartPlugin.ThreadId = input.ThreadId;
        var chartDefinition = new ChartPluginDefinition(_chartPlugin);

        List<AIFunction> aiFunctions =
        [
            AIFunctionFactory.Create(chartDefinition.PlotScatterAsync),
            AIFunctionFactory.Create(chartDefinition.PlotTimeSeriesData),
            AIFunctionFactory.Create(chartDefinition.PlotPieChartAsync),
            AIFunctionFactory.Create(chartDefinition.PlotBarChartAsync)
        ];
        
        var matchingTool = aiFunctions.Single(x => x.Name == input.FunctionCallContent.Name);

        try
        {
            // Invoke the function
            var invokeResult = await matchingTool.InvokeAsync(input.FunctionCallContent.Arguments);
            var result = new FunctionResultContent(input.FunctionCallContent.CallId, invokeResult);

            // Return successful result
            return new ExecuteActionOutput(
                ChatMessage: new ChatMessage(ChatRole.Tool, [result]),
                Succeeded: true,
                Is202Submit: false);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Function tool invocation failed.");

            // Handle all errors with a single catch
            string errorMessage = $"Error executing {input.FunctionCallContent?.Name ?? "function"}: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $" | Details: {ex.InnerException.Message}";
            }

            // Return error as function result so it appears in chat
            var errorResult = new FunctionResultContent(
                input.FunctionCallContent?.CallId ?? "error",
                errorMessage);

            return new ExecuteActionOutput(
                ChatMessage: new ChatMessage(ChatRole.Tool, [errorResult]),
                Succeeded: false,
                Is202Submit: false);
        }
    }
}

