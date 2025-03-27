using FirstPartyAgent.Models;

namespace FirstPartyAgent.Core.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class AgentPromptAttribute : Attribute
    {
        public string Description { get; }
        public AgentMode AgentMode { get; }

        public AgentPromptAttribute(string description, AgentMode agentMode)
        {
            Description = description;
            AgentMode = agentMode;
        }
    }

}
