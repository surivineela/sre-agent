#  Gremlin Emulator

Provides a local replacement for Cosmos DB (Gremlin) that can be used in evals, using Apache Tinkerpop running in a container.

## Running Locally

Use `just run-gremlin` or `src/run-gremlin-emulator.sh` to start the emulator. It will automatically discover and load all `.graphml` files in the graphs directory. The emulator will start listening on port 8182.

Tests can setup a gremlin client pointing at the emulator using:

    services.AddLocalGremlin(graphName);

Where `graphName` is the traversal source for the graph you want your test to use (see individual graph sections for the correct names).

**Note:** Graph names and traversal sources are now generated automatically from filenames using camelCase conversion (e.g., `my-awesome-graph.graphml` → graph: `myAwesomeGraph`, traversal: `gmyAwesomeGraph`).

## Adding a new Graph

1. Prepare your knowledge graph by configuring your crawlroots and running the crawler and if necessary, making your own edits. Consider dropping everything from your graph beforehand with `g.V().drop()` to make sure you're not exporting unrelated stuff.

1. Export your graph and start the emulator:
   ```bash
   just export-graph-to-gremlin my-awesome-graph
   ```
   The system will automatically:
   - Export your graph to `my-awesome-graph.graphml`
   - Discover the file and all other existing graphs
   - Generate graph name `myAwesomeGraph` 
   - Create traversal source `gmyAwesomeGraph`
   - Load all graphs and (re)start the emulator
   
   Your new graph will be available to use in tests:
   `services.AddLocalGremlin("gmyAwesomeGraph");`

1. Assuming you'll commit this new graph to the repo, consider updating this readme with a new section that explains the contents of the graph you exported (see below).

## Graphs

### Functions Bad Flex Apps

The graph `functions-bad-flex-apps.graphml` is based on a crawl of various flex consumption function apps, most of which have some sort of configuration error:
https://dev.azure.com/msazure/One/_git/AAPT-SREAgent-Tests?path=/src/Functions.SREAgent.Tests/bad-apps

Graph was last updated on 2025-05-13 using:
https://dev.azure.com/msazure/One/_git/AAPT-SREAgent-Tests 57e4cda7945247c0c1d4625a720e16d04a468b38
https://dev.azure.com/msazure/One/_git/AAPT-Antares-OperationalAgent 313601e0fa1674b51b9a644628251b4ed2b7bddf

Use graphname `gfunctionsBadFlexApps` to connect to this graph.

### Simple Web Apps

The graph `simple-webapps.graphml` is based on a crawl of two resource groups with a total of 4 webapps.

1. `pbatum-sre-web-eas1`
- **Location:** eastasia  
- **SKU:** Standard (S1)  
- **Resource Group:** `pbatum-sre-web-eas`  
- **App Service Plan:** `ASP-pbatumsrewebeas-8754` (shared with `pbatum-sre-web-eas2`)  
- **Notes:** Runs on 1 instance.

2. `pbatum-sre-web-eas2`
- **Location:** eastasia  
- **SKU:** Standard (S1)  
- **Resource Group:** `pbatum-sre-web-eas`  
- **App Service Plan:** `ASP-pbatumsrewebeas-8754` (shared with `pbatum-sre-web-eas1`)

3. `pbatum-sre-web-eas3`
- **Location:** eastasia  
- **SKU:** Premium0V3 (P0v3)  
- **Resource Group:** `pbatum-sre-web-eas-lin`  
- **App Service Plan:** `ASP-pbatumsrewebeaslin-91de` (shared with `pbatum-sre-web-eas4`)  
- **Notes:** Linux web app.

4. `pbatum-sre-web-eas4`
- **Location:** eastasia  
- **SKU:** Premium0V3 (P0v3)  
- **Resource Group:** `pbatum-sre-web-eas-lin`  
- **App Service Plan:** `ASP-pbatumsrewebeaslin-91de` (shared with `pbatum-sre-web-eas3`)  
- **Notes:** Linux web app.

Graph was last updated on 2025-05-14 using:
https://dev.azure.com/msazure/One/_git/AAPT-Antares-OperationalAgent b19d0afddbe6f59866a1d90c1a5cb352d95d26b4

Use graphname `gsimpleWebapps` to connect to this graph.

### Func Scenarios 1

The graph `func-scenarios1.graphml` is based on a crawl of a subscription named "Private Test Sub CURIBE". It includes several function apps and related resources within the "mybuggyfunctionapp-rg" resource group. Key applications and components represented in this graph are:

- `mybuggyfunctionapp`
- `alsobuggyfunctionapp`
- `notafunctionapp` (Note: this is a web app ).

The graph primarily contains `microsoft.insights/components` (Application Insights) and `microsoft.alertsmanagement/smartdetectoralertrules` associated with these applications.

Graph was last updated on 2025-05-22.

Use graphname `gfuncScenarios1` to connect to this graph.

### Container Apps Memory Graph

The graph `mrsharm-aca-diag.graphml` is based on a crawl of Azure Container Apps (ACA) resources that are on mrsharm's sub for diagnostic purposes. The graph contains:

- **Container App:** `diagnosticbench-app-202504091010` in resource group `mrsharm-operations-agent-3p-rg`
- **Managed Environment:** `approvalservice` in resource group `approval-service-rg`
- **Multiple Revisions:** Various revisions of the diagnostic bench app with different suffixes
- **Location:** eastus2

This graph focuses on Azure Container Apps infrastructure and is useful for testing ACA-related diagnostics and scenarios.

Graph was last updated on 2025-06-23.

Use graphname `gmrsharmAcaDiag` to connect to this graph.





