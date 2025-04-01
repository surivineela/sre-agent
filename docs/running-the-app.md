# Running the Application

1. Open the solution:
   ```powershell
   .\AAPT-Antares-OperationalAgent\src\Agent>Agent.sln
   ```

2. Update  `appsettings.json` under Agent.Web with your env prefix and add a crawler root.
Tip: Set the crawl root to a resource group instead of your subscription. This will keep your graph small and help you test things better. 
```
 "AppSettings": {
  "EnvPrefix": "yourprefixfromdeployment", 
 }

  "Crawler": {
    "CrawlRoots": "/subscriptions/de22a777-a2a3-44e6-917f-b36c8246fea5/resourceGroups/ca-stacyzeng-group",
  }
```

3. Build and run the solution

4. The `Agent.Web` project will start a test chat client using your identity to access Azure resources.
   
![Project Demo](images/Project.gif)

[Back to Development Setup](development-setup.md) | [Next: Graph Database Setup](graph-database.md) 


# Dealing With Workflow Approvals
The approval workflow looks like this:

::: mermaid
graph TD
Chat-Site --> |Give Approval URL| User
User -->|Approve| Approval-Site
Approval-Site -->|Register Decision|Chat-Site
:::

There are two ways to handle this in the local environment:

## Run the approval site locally
1. Set the env var `AGENT_ENDPOINT` to your local agent chat URL - likely `https://localhost:7023`. Omit the trailing `/`!
1. Open a new instance of VS and load the solution `OperationalAgent.Approval`
1. Run the single project in there, and note the URL (likely `https://localhost:7268/`)
1. Add this URL to your appsettings.developer.json file in the main solution: `"ApprovalsEndpoint": "https://localhost:7268/",`

Now all approvals will go through your local site and the callback to your local agent UI will work.

Note: Often the approval link won't show up in the chat. In that case, you can find it in the console log - it will look like this:

`Approval link generated: "https://localhost:7268/?data=eyJhc...R9zdDo3MDIzLyJ9" for "approval-26be26ad-aaf2-52c1-97ae-78fc29684bc4". Trying to notify user.`

## Fake it
You can avoid the above by manually finding the pending approvals and manually making the call to the local approval endpoint:

`GET  {{host}}/api/v1/approvals`

Grab the ID from that response.
Then:

``` 
POST {{host}}/api/v1/approvals/{{id}}/decision
{ "user": "Myself", "status": "Approved" }
```
