// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Web.Services;

public class AgentYamlTranslatorFactory : IAgentYamlTranslatorFactory
{
    public IAgentYamlTranslator GetTranslator(string apiVersion) => apiVersion switch
    {
        "agent.platform.ai/v1" => new AgentYamlTranslatorV1(),
        _ => throw new NotSupportedException($"Unsupported apiVersion: {apiVersion}")
    };
}
