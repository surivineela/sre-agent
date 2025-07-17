// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Agent.Data.DatabaseClients.GraphDbClient;

public class FunctionNode: ArmResourceNode
{
    public string TriggerType { get; set; }

    public string? QueueName { get; set; }

    public string? EventHubName { get; set; }
    public string? ServiceBusQueueName { get; set; }
    public string? ServiceBusTopicName { get; set; }
    public Dictionary<string, object> BindingDetails { get; set; }
    public Dictionary<string, object>? ScalingDetails { get; set; }
    public Dictionary<string, string>? RuntimeInfo { get; set; }
    public Dictionary<string, object>? PerformanceCharacteristics { get; set; }
    public Dictionary<string, object>? OperationalMetadata { get; set; }
    public Dictionary<string, object>? MonitoringSettings { get; set; }

    public FunctionNode(Function function)
        : base("microsoft.web/sites/functions", function.Id, function.SubscriptionId, function.ResourceGroupName, function.Name, location: function.Location)
    { }

    public class Function
    {
        public string Id { get; set; }
        public string SubscriptionId { get; set; }
        public string ResourceGroupName { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public string TriggerType { get; set; }

        public string? QueueName { get; set; }

        public string? EventHubName { get; set; }
        public string? ServiceBusQueueName { get; set; }
        public string? ServiceBusTopicName { get; set; }
        public Dictionary<string, object> BindingDetails { get; set; }
        public Dictionary<string, object>? ScalingDetails { get; set; }
        public Dictionary<string, string>? RuntimeInfo { get; set; }
        public Dictionary<string, object>? PerformanceCharacteristics { get; set; }
        public Dictionary<string, object>? OperationalMetadata { get; set; }
        public Dictionary<string, object>? MonitoringSettings { get; set; }
    }
}
