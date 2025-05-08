namespace Agent.Runtime.SubAgents.FunctionAppDiagnosticsAgent
{
    public class FunctionAppDiagnosticsRoutingOutput
    {
        public bool ResultParsed { get; set; }
        public FunctionAppDiagnosticsAgentType AgentType { get; set; }

        public FunctionAppDiagnosticsRoutingOutput(bool resultParsed, FunctionAppDiagnosticsAgentType agentType)
        {
            ResultParsed = resultParsed;
            AgentType = agentType;
        }
    }
}
