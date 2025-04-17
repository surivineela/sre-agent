using Agent.Core.Models.Api.v1;

namespace Agent.Core.Helpers;

public class AgentTypeHelper
{
    public static bool IsScannerAgent(AgentTypeEnum agentType)
    {
        return agentType == AgentTypeEnum.SourceCode
            || agentType == AgentTypeEnum.CVE;
    }
}
