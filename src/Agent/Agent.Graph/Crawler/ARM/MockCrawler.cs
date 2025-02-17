using Agent.Graph.Schema;

namespace Agent.Graph.Crawler.ARM
{
    public class MockCrawler
    {
        public static async Task CrawlMock(InMemoryGraphManager inMemoryGraphManager)
        {

            // Subscriptions

            const string Sub1 = nameof(Sub1);
            const string Sub2 = nameof(Sub2); // Used by bad app
            var goodSubNode = new Node(
                id: $"/subscriptions/{Sub1}",
                name: Sub1,
                type: "Subscription");
            inMemoryGraphManager.AddOrUpdateNode(goodSubNode);

            var badSubNode = new Node(
                id: $"/subscriptions/{Sub2}",
                name: Sub2,
                type: "Subscription");
            inMemoryGraphManager.AddOrUpdateNode(badSubNode);

            // Resource Groups

            const string RG1 = nameof(RG1);
            const string RG2 = nameof(RG2); // Used by bad app
            var goodRgNode = new Node(
                id: $"/subscriptions/{Sub1}/resourceGroups/{RG1}",
                name: RG1,
                type: "ResourceGroup");
            inMemoryGraphManager.AddOrUpdateNode(goodRgNode);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: goodSubNode,
                targetNode: goodRgNode,
                relationshipType: "contains");

            var badRgNode = new Node(
                id: $"/subscriptions/{Sub2}/resourceGroups/{RG2}",
                name: RG2,
                type: "ResourceGroup");
            inMemoryGraphManager.AddOrUpdateNode(badRgNode);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: badSubNode,
                targetNode: badRgNode,
                relationshipType: "contains");

            // App Service Plans
            const string Asp1 = nameof(Asp1);
            const string Asp2 = nameof(Asp2); // Used by bad app
            var goodAspNode = new Node(
                id: $"/subscriptions/{Sub1}/resourceGroups/{RG2}/providers/Microsoft.Web/serverFarms/{Asp2}",
                name: Asp1,
                type: "App Service Plan");
            inMemoryGraphManager.AddOrUpdateNode(goodAspNode);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: goodRgNode,
                targetNode: goodAspNode,
                relationshipType: "contains");

            var badAspNode = new Node(
                id: $"/subscriptions/{Sub1}/resourceGroups/{RG2}/providers/Microsoft.Web/serverFarms/{Asp2}",
                name: Asp2,
                type: "App Service Plan");
            inMemoryGraphManager.AddOrUpdateNode(badAspNode);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: badRgNode,
                targetNode: badAspNode,
                relationshipType: "contains");

            // Sql Servers

            const string SqlServer1 = nameof(SqlServer1);
            const string SqlServer2 = nameof(SqlServer2); // Used by bad app
            var goodSqlServerNode = new Node(
                id: $"/subscriptions/{Sub1}/resourceGroups/appservices-sre-demo/providers/Microsoft.Sql/servers/oa-demo-sql-stage",
                name: SqlServer1,
                type: "SqlServer");
            inMemoryGraphManager.AddOrUpdateNode(goodSqlServerNode);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: goodRgNode,
                targetNode: goodSqlServerNode,
                relationshipType: "contains");

            var badSqlServerNode = new Node(
                id: $"/subscriptions/{Sub2}/resourceGroups/appservices-sre-demo/providers/Microsoft.Sql/servers/oa-demo-sql-stage",
                name: SqlServer2,
                type: "SqlServer");
            inMemoryGraphManager.AddOrUpdateNode(badSqlServerNode);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: badRgNode,
                targetNode: badSqlServerNode,
                relationshipType: "contains");
            
            // Web Apps

            const string App1 = nameof(App1);
            const string App2 = nameof(App2);
            const string App3 = nameof(App3);
            const string App4 = nameof(App4);
            const string App5 = nameof(App5);
            var webApp1Node = new Node(
                id: $"/subscriptions/{Sub1}/resourceGroups/{RG1}/providers/Microsoft.Web/sites/{App1}",
                name: App1,
                type: "WebApp");
            inMemoryGraphManager.AddOrUpdateNode(webApp1Node);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: goodAspNode,
                targetNode: webApp1Node,
                relationshipType: "contains");
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: webApp1Node,
                targetNode: goodSqlServerNode,
                relationshipType: "uses");

            var webApp2Node = new Node(
                id: $"/subscriptions/{Sub1}/resourceGroups/{RG1}/providers/Microsoft.Web/sites/{App2}",
                name: App2,
                type: "WebApp");
            inMemoryGraphManager.AddOrUpdateNode(webApp2Node);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: goodAspNode,
                targetNode: webApp2Node,
                relationshipType: "contains");
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: webApp2Node,
                targetNode: goodSqlServerNode,
                relationshipType: "uses");

            var webApp3Node = new Node(
                id: $"/subscriptions/{Sub1}/resourceGroups/{RG1}/providers/Microsoft.Web/sites/{App3}",
                name: App3,
                type: "WebApp");
            inMemoryGraphManager.AddOrUpdateNode(webApp3Node);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: goodAspNode,
                targetNode: webApp3Node,
                relationshipType: "contains");
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: webApp3Node,
                targetNode: goodSqlServerNode,
                relationshipType: "uses");

            var webApp4Node = new Node(
                id: $"/subscriptions/{Sub2}/resourceGroups/{RG2}/providers/Microsoft.Web/sites/{App4}",
                name: App4,
                type: "WebApp");
            inMemoryGraphManager.AddOrUpdateNode(webApp4Node);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: badAspNode,
                targetNode: webApp4Node,
                relationshipType: "contains");
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: webApp4Node,
                targetNode: badSqlServerNode,
                relationshipType: "uses");

            // App 5 will not have a corresponding managed identity and thus be the "bad" app
            // This is intentional to simulate a scenario where an app lacks a managed identity
            var webApp5Node = new Node(
                id: $"/subscriptions/{Sub2}/resourceGroups/{RG2}/providers/Microsoft.Web/sites/{App5}",
                name: App5,
                type: "WebApp");
            inMemoryGraphManager.AddOrUpdateNode(webApp5Node);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: badAspNode,
                targetNode: webApp5Node,
                relationshipType: "contains");
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: webApp5Node,
                targetNode: badSqlServerNode,
                relationshipType: "uses");

            // Managed Identities

            var webApp1ManagedIdentity = new Node(
                id: $"/subscriptions/{Sub1}/resourceGroups/{RG1}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{App1}",
                name: "goodApp1ManagedIdentity",
                type: "ManagedIdentity");
            inMemoryGraphManager.AddOrUpdateNode(webApp1ManagedIdentity);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: webApp1Node,
                targetNode: webApp1ManagedIdentity,
                relationshipType: "has");

            var webApp2ManagedIdentity = new Node(
                id: $"/subscriptions/{Sub1}/resourceGroups/{RG1}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{App2}",
                name: "goodApp2ManagedIdentity",
                type: "ManagedIdentity");
            inMemoryGraphManager.AddOrUpdateNode(webApp2ManagedIdentity);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: webApp2Node,
                targetNode: webApp2ManagedIdentity,
                relationshipType: "has");

            var webApp3ManagedIdentity = new Node(
                id: $"/subscriptions/{Sub1}/resourceGroups/{RG1}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{App3}",
                name: "goodApp3ManagedIdentity",
                type: "ManagedIdentity");
            inMemoryGraphManager.AddOrUpdateNode(webApp3ManagedIdentity);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: webApp3Node,
                targetNode: webApp3ManagedIdentity,
                relationshipType: "has");

            var webApp4ManagedIdentity = new Node(
                id: $"/subscriptions/{Sub2}/resourceGroups/{RG2}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{App4}",
                name: "goodApp4ManagedIdentity",
                type: "ManagedIdentity");
            inMemoryGraphManager.AddOrUpdateNode(webApp4ManagedIdentity);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: webApp4Node,
                targetNode: webApp4ManagedIdentity,
                relationshipType: "has");


            // Roles

            var goodRoleNode = new Node(
                id: $"/subscriptions/{Sub1}/resourceGroups/{RG1}/providers/Microsoft.Authorization/roleDefinitions/role1",
                name: "goodRole",
                type: "Role");
            inMemoryGraphManager.AddOrUpdateNode(goodRoleNode);

            // Role Assignments

            var goodApp1RoleAssignment = new Node(
                id: $"/subscriptions/{Sub1}/resourceGroups/{RG1}/providers/Microsoft.Authorization/roleAssignments/{App1}",
                name: "goodApp1RoleAssignment",
                type: "RoleAssignment");
            inMemoryGraphManager.AddOrUpdateNode(goodApp1RoleAssignment);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: goodRoleNode,
                targetNode: goodApp1RoleAssignment,
                relationshipType: "has");
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: goodApp1RoleAssignment,
                targetNode: webApp1ManagedIdentity,
                relationshipType: "assignedTo");

            var goodApp2RoleAssignment = new Node(
                id: $"/subscriptions/{Sub1}/resourceGroups/{RG1}/providers/Microsoft.Authorization/roleAssignments/{App2}",
                name: "goodApp2RoleAssignment",
                type: "RoleAssignment");
            inMemoryGraphManager.AddOrUpdateNode(goodApp2RoleAssignment);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: goodRoleNode,
                targetNode: goodApp2RoleAssignment,
                relationshipType: "has");
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: goodApp2RoleAssignment,
                targetNode: webApp2ManagedIdentity,
                relationshipType: "assignedTo");

            var goodApp3RoleAssignment = new Node(
                id: $"/subscriptions/{Sub1}/resourceGroups/{RG1}/providers/Microsoft.Authorization/roleAssignments/{App3}",
                name: "goodApp3RoleAssignment",
                type: "RoleAssignment");
            inMemoryGraphManager.AddOrUpdateNode(goodApp3RoleAssignment);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: goodRoleNode,
                targetNode: goodApp3RoleAssignment,
                relationshipType: "has");
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: goodApp3RoleAssignment,
                targetNode: webApp3ManagedIdentity,
                relationshipType: "assignedTo");

            var goodApp4RoleAssignment = new Node(
                id: $"/subscriptions/{Sub2}/resourceGroups/{RG2}/providers/Microsoft.Authorization/roleAssignments/{App4}",
                name: "goodApp4RoleAssignment",
                type: "RoleAssignment");
            inMemoryGraphManager.AddOrUpdateNode(goodApp4RoleAssignment);
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: goodRoleNode,
                targetNode: goodApp4RoleAssignment,
                relationshipType: "has");
            inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                sourceNode: goodApp4RoleAssignment,
                targetNode: webApp4ManagedIdentity,
                relationshipType: "assignedTo");
        }
    }
}
