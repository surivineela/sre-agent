// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Agent.Data.DatabaseClients.GraphDbClient;

public class WorkflowNode : ArmResourceNode
{
    public WorkflowNode(Workflow workflow)
        : base("microsoft.web/sites/workflows", workflow.Id, workflow.SubscriptionId, workflow.ResourceGroupName, workflow.Name, location: workflow.Location)
    { }

    public class Workflow
    {
        public string Id { get; set; }
        public string SubscriptionId { get; set; }
        public string ResourceGroupName { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
    }
}
