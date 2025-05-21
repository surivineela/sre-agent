using System.ComponentModel;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common;
namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent
{
    public record ContainerAppRevisionAgentActivityInput : BaseContainerAppIssueActivityInput
    {
        [Description("The name of the container app.")]
        public string ContainerAppName { get; init; } = string.Empty;

        [Description("The revision name of the container app.")]
        public string RevisionName { get; init; } = string.Empty;
    }
}
