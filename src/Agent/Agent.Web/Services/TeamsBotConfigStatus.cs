namespace Agent.Web.Services
{
    /// <summary>
    /// Interface to track the status of Teams Bot configuration
    /// </summary>
    public interface ITeamsBotConfigStatus
    {
        bool IsConfigured { get; }
    }

    /// <summary>
    /// Implementation to track the status of Teams Bot configuration
    /// </summary>
    public class TeamsBotConfigStatus : ITeamsBotConfigStatus
    {
        public bool IsConfigured { get; set; }
    }
}
