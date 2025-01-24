namespace PerformanceIssues.Serivces
{
    public class EventManager : IEventManager
    {
        private readonly List<WeakReference> _subscribers = new();
        private readonly List<Action<string>> _strongSubscribers = new();  // Intentional memory leak

        public void Subscribe(Action<string> handler)
        {
            // Memory leak: storing both weak and strong references
            _subscribers.Add(new WeakReference(handler));
            _strongSubscribers.Add(handler);  // This prevents garbage collection
        }

        public void RaiseEvent(string message)
        {
            foreach (var weakRef in _subscribers.ToList())
            {
                if (weakRef.Target is Action<string> handler)
                {
                    handler(message);
                }
            }

            foreach (var handler in _strongSubscribers)
            {
                handler(message);
            }
        }
    }
}
