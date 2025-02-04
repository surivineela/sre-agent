This approval app has two flows - see Program.cs to update the route as needed

The first flow is what Shekhar originally leveraged alongside durable functions ctx. Start this web app and add ?action_name={VALUE} where VALUE maps to specific DF orchestration names. From within DF we call ctx.WaitForExternalEvent which waits for a POST from this web app with hardcoded event name.

The second flow stores approvals in memory & exposes a new api /GetApprovals that can be polled. Start this web app and add ?action_name={VALUE} where VALUE can be any string (for example, random guid per approval request). The approvals are reset at web app restart. In this model the client polls /GetApprovals. 

# Sample code for second flow:
```csharp
using OperationalAgentRuntime.Helpers;
using System.Text.Json;

var approvalWebService = "https://localhost:7268";
var approvalId = "sample-approval-id";
Console.WriteLine($"Waiting for approval id {approvalId} - navigate to {approvalWebService}?action_name={approvalId}");

var approval = await ApprovalHelper.WaitForApprovalAsync(approvalWebService, "sample-approval-id", 5, 120);
Console.WriteLine(JsonSerializer.Serialize(approval));
```

# Local Settings for first flow
Update the appsettings.Development.json with the following:
"OperationalRuntimeSendEventEndpoint": "http://localhost:7253/runtime/webhooks/durabletask/instances/{0}/raiseEvent/{1}?code=<runtime_code>


You can get the runtime_code when you run ProcessMesasge http trigger in OperationalAgentRuntime.

