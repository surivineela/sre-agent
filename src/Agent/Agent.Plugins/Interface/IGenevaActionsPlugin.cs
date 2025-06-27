

namespace Agent.Plugins.Interface
{
    public interface IGenevaActionsPlugin
    {
        Guid? ThreadId { get; set; }

        Task<string> ListInputParametersForGenevaAction(string actionName);

        Task<string> ExecuteGenevaAction(string incidentId, string actionName, Dictionary<string, string> inputParameters);
    }
}
