namespace Agent.Core.Helpers;

public class AgentTypeHelper
{
    public static bool IsScannerAgent(AgentTypeEnum agentType)
    {
        return agentType == AgentTypeEnum.SourceCodeAgent
            || agentType == AgentTypeEnum.CVEAgent;
    }
}
