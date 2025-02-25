namespace Agent.Plugins
{
    public interface ICurrentStatePlugin
    {
        string GetCurrentAppState(string appName);

        Task<string> GetCurrentBotState();
    }
}
