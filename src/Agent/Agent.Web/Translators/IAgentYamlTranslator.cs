// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Web.Models.ExtendedAgents;

namespace Agent.Web.Services;


public interface IAgentYamlTranslator
{
    AgentConfigurationDocumentModel Translate(AgentDeploymentModel model);
}
