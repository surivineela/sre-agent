// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using Agent.Web.Models.ExtendedAgents;

namespace Agent.Web.Services;



public class YamlValidatorV1 : IYamlValidator
{
    public void Validate(AgentDeploymentModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Spec.Name))
            throw new ValidationException("Agent name is required.");

        // Tools in the spec are just string names/references, not full tool definitions
        // So we can't validate tool-specific properties here
        foreach (var toolName in model.Spec.Tools)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                throw new ValidationException("Tool name cannot be empty.");
        }
    }
}

