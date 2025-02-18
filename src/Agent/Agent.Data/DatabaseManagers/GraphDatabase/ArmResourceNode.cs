namespace Agent.Data.DatabaseManagers.GraphDatabase
{
    public interface IArmResourceGraphNode
    {
        public string GetNodeLabel();
        public string GetNodeId();
        public string GetResourceType();
        public IDictionary<string, object> GetNodeProperties();
    }

    public class ArmResourceNode : IArmResourceGraphNode
    {
        public string ResourceType { get; set; }
        public string ResourceId { get; set; }
        public string SubscriptionId { get; set; }
        public string ResourceGroupName { get; set; }
        public string ResourceName { get; set; }

        public ArmResourceNode() { }
        public ArmResourceNode(string resourceType,
            string resourceId,
            string subscriptionId,
            string resourceGroupName,
            string resourceName)
        {
            ResourceType = resourceType;
            ResourceId = resourceId;
            SubscriptionId = subscriptionId;
            ResourceGroupName = resourceGroupName;
            ResourceName = resourceName;
        }

        public virtual string GetNodeLabel()
        {
            var parts = ResourceType.Split('/');
            return parts[parts.Length-1];
        }

        public virtual string GetNodeId()
        {
            return ResourceId;
        }

        public virtual string GetResourceType()
        {
            return ResourceType;
        }

        public virtual IDictionary<string, object> GetNodeProperties()
        {
            return new Dictionary<string, object>
            {
                // resourceType is partition key, cannot be updated
                //{ "resourceType", ResourceType },
                { "resourceId", ResourceId },
                { "subscriptionId", SubscriptionId },
                { "resourceGroupName", ResourceGroupName },
                { "resourceName", ResourceName }
            };
        }
    }

    public sealed class SubscriptionNode : ArmResourceNode
    {
        public SubscriptionNode() { }

        public SubscriptionNode(string subscriptionId) : base()
        {
            ResourceType = "Subscription";
            SubscriptionId = subscriptionId;
            ResourceName = subscriptionId;
            ResourceId = subscriptionId;
        }

        public override IDictionary<string, object> GetNodeProperties()
        {
            return new Dictionary<string, object> { };
        }
    }


    public sealed class ContainerAppEnvironmentNode : ArmResourceNode
    {
        public string Location { get; set; }
        public string? VnetId { get; set; }
        public string? LbId { get; set; }

        public ContainerAppEnvironmentNode() : base() { }

        public ContainerAppEnvironmentNode(string resourceType,
            string resourceId,
            string subscriptionId,
            string resourceGroupName,
            string resourceName,
            string location,
            string? vnetId = null,
            string? lbId = null)
            : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName)
        {
            Location = location;
            VnetId = vnetId;
            LbId = lbId;
        }

        public override IDictionary<string, object> GetNodeProperties()
        {
            var props = base.GetNodeProperties();
            props.Add("location", Location);
            if (VnetId != null)
            {
                props.Add("vnetId", VnetId);
            }
            if (LbId != null)
            {
                props.Add("lbId", LbId);
            }

            return props;
        }
    }
}
