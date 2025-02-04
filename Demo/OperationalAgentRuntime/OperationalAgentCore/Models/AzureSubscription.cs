namespace OperationalAgentCore
{
    public class AzureSubscription
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public List<string> Resources { get; set; }

        public AzureSubscription(string id, string name, List<string> resources)
        {
            this.Id = id;
            this.Name = name;
            this.Resources = resources ?? new List<string>();
        }
    }
}
