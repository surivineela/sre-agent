using Agent.Framework;

namespace Agent.Cli.Validations;

public static class AgentDescriptorValidation
{
    public static void ValidateAgentDescriptor(IAgentDescriptor? agentDescriptor, out List<string> errors)
    {
        errors = new List<string>();
        
        if (agentDescriptor is null)
        {
            errors.Add("Agent descriptor is null.");
            return;
        }

        if (string.IsNullOrEmpty(agentDescriptor.Name))
        {
            errors.Add($"Agent descriptor {agentDescriptor.GetType().Name} does not have a name.");
        }

        if (string.IsNullOrEmpty(agentDescriptor.Instructions))
        {
            errors.Add($"Agent descriptor {agentDescriptor.Name} does not have instructions.");
        }

        if (agentDescriptor.Tools == null || agentDescriptor.Tools.Count == 0)
        {
            errors.Add($"Agent descriptor {agentDescriptor.Name} must have at least one tool.");
        }
        else
        {
            foreach (var tool in agentDescriptor.Tools)
            {
                if (string.IsNullOrWhiteSpace(tool))
                {
                    errors.Add("Tool name cannot be empty.");
                }
                else if (tool.Any(char.IsWhiteSpace))
                {
                    errors.Add($"Tool name '{tool}' must not contain whitespace.");
                }
            }
        }

        // Validate name doesn't contain whitespace
        if (!string.IsNullOrEmpty(agentDescriptor.Name) && agentDescriptor.Name.Any(char.IsWhiteSpace))
        {
            errors.Add($"Agent name '{agentDescriptor.Name}' must not contain whitespace.");
        }

        // Validate handoffs
        if (agentDescriptor.Handoffs != null)
        {
            foreach (var handoff in agentDescriptor.Handoffs)
            {
                if (string.IsNullOrWhiteSpace(handoff))
                {
                    errors.Add("Handoff name cannot be empty.");
                }
                else if (handoff.Any(char.IsWhiteSpace))
                {
                    errors.Add($"Handoff name '{handoff}' must not contain whitespace.");
                }
            }
        }

        // Validate temperature range if provided
        if (agentDescriptor.Temperature.HasValue && (agentDescriptor.Temperature.Value < 0 || agentDescriptor.Temperature.Value > 2))
        {
            errors.Add("Temperature must be between 0 and 2.");
        }

        // Validate max reflection count
        if (agentDescriptor.MaxReflectionCount < 0)
        {
            errors.Add("Max reflection count cannot be negative.");
        }

        // Validate instructions length
        if (!string.IsNullOrEmpty(agentDescriptor.Instructions))
        {
            if (agentDescriptor.Instructions.Length < 50)
            {
                errors.Add("System prompt must be longer than 50 characters.");
            }
            else if (agentDescriptor.Instructions.Length > 5000)
            {
                errors.Add("System prompt must be under 5000 characters.");
            }
        }

        // Validate handoff description if provided
        if (!string.IsNullOrWhiteSpace(agentDescriptor.HandoffDescription) && agentDescriptor.HandoffDescription.Length > 500)
        {
            errors.Add("Handoff description must be under 500 characters.");
        }

        // Validate common prompts
        if (agentDescriptor.CommonPrompts != null)
        {
            foreach (var prompt in agentDescriptor.CommonPrompts)
            {
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    errors.Add("Common prompt name cannot be empty.");
                }
            }
        }

        // Validate agents as tools
        if (agentDescriptor.AgentsAsTools != null)
        {
            foreach (var agentTool in agentDescriptor.AgentsAsTools)
            {
                if (string.IsNullOrWhiteSpace(agentTool.AgentName))
                {
                    errors.Add("Agent name in agents_as_tools cannot be empty.");
                }
                if (string.IsNullOrWhiteSpace(agentTool.ToolName))
                {
                    errors.Add("Tool name in agents_as_tools cannot be empty.");
                }
                if (string.IsNullOrWhiteSpace(agentTool.ToolDescription))
                {
                    errors.Add("Tool description in agents_as_tools cannot be empty.");
                }
            }
        }
    }
}
