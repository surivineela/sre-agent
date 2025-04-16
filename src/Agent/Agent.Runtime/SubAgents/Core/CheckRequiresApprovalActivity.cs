// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Attributes;
using Microsoft.DurableTask;
using System.Reflection;

namespace Agent.Runtime.SubAgents.Core;

[DurableTask]
public class CheckRequiresApprovalActivity : TaskActivity<(IReadOnlyList<string>, string), bool>
{
    private readonly ToolsRepository _toolsRepository;

    public CheckRequiresApprovalActivity(ToolsRepository toolsRepository)
    {
        _toolsRepository = toolsRepository;
    }

    public override Task<bool> RunAsync(TaskActivityContext context, (IReadOnlyList<string>, string) input)
    {
        try
        {
            (var toolSignatures, var targetFunction) = input;
            // Get all tools and find matching tool
            var aiFunctions = _toolsRepository.GetAllTools(toolSignatures).Select(_toolsRepository.FindAiFunction);
            var matchingTool = aiFunctions.Single(x => x.ToolFunction.Name == targetFunction);

            // Check if requiers approval
            bool requiresApproval = matchingTool.ToolFunction.UnderlyingMethod?.GetCustomAttribute<RequiresApprovalAttribute>() != null;
            return Task.FromResult(requiresApproval);
        }
        catch (Exception)
        {
            return Task.FromResult(false);
        }
    }
}
