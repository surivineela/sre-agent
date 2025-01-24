namespace PerformanceIssues.Serivces
{
    public interface IEventManager
    {
        void Subscribe(Action<string> handler);
        void RaiseEvent(string message);
    }
}
