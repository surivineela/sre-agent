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
        if (string.IsNullOrWhiteSpace(model.Spec.Agent.Name))
            throw new ValidationException("Agent name is required.");

        foreach (var tool in model.Spec.Tools)
        {
            if (tool is KustoToolApiModel kusto)
            {
                if (string.IsNullOrWhiteSpace(kusto.Function))
                    throw new ValidationException($"KustoTool '{kusto.Name}' is missing Function.");

            }
        }
    }
}

