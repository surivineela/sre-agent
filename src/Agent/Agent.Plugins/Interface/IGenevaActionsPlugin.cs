namespace Agent.Plugins.Interface
{
    public interface IGenevaActionsPlugin
    {
        Task<string> ListInputParametersForGenevaAction(string actionName);

        Task<string> ExecuteGenevaAction(string actionName, Dictionary<string, string> inputParameters);
    }
}