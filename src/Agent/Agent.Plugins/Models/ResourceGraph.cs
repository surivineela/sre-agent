namespace Agent.Plugins.Models
{
    public class ResourceGraph
    {
        private readonly List<Resource> _resources = new List<Resource>();

        public void AddResources(IEnumerable<Resource> resources)
        {
            _resources.AddRange(resources);
        }

        public IReadOnlyList<Resource> Resources => _resources.AsReadOnly();
    }
}
