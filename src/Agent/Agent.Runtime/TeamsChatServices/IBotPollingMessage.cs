namespace Agent.Runtime.TeamsChatServices
{
    /// <summary>
    /// Interface for bot implementations that support message polling
    /// </summary>
    public interface IBotPollingMessage
    {
        /// <summary>
        /// Start polling for new messages
        /// </summary>
        void StartMessagePolling();

        /// <summary>
        /// Stop polling for messages
        /// </summary>
        void StopMessagePolling();
    }
}
