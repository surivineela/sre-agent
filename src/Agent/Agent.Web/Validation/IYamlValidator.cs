// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Web.Models.ExtendedAgents;

namespace Agent.Web.Services;



public interface IYamlValidator
{
    void Validate(AgentDeploymentModel model);
}
