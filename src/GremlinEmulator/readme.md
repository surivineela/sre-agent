#  Gremlin Emulator

Provides a local replacement for Cosmos DB (Gremlin) that can be used in evals, using Apache Tinkerpop running in a container.

## Running Locally

Use `run-gremlin-emulator.sh` to start the emulator. It will start listening on port 8182.

Tests can setup a gremlin client pointing at the emulator using:

    services.AddLocalGremlin(graphName);

Where `graphName` is the traversal source for the graph you want your test to use e.g. `gfuncbad`

## Graphs

### Functions Bad Flex Apps

The graph `functions-bad-flex-apps.graphml` is based on a crawl of various flex consumption function apps, most of which have some sort of configuration error:
https://dev.azure.com/msazure/One/_git/AAPT-SREAgent-Tests?path=/src/Functions.SREAgent.Tests/bad-apps

Graph was last updated on 2025-05-13 using:
https://dev.azure.com/msazure/One/_git/AAPT-SREAgent-Tests 57e4cda7945247c0c1d4625a720e16d04a468b38
https://dev.azure.com/msazure/One/_git/AAPT-Antares-OperationalAgent 313601e0fa1674b51b9a644628251b4ed2b7bddf

Use graphname `gfuncbad` to connect to this graph.

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

Use graphname `gsimpleweb` to connect to this graph.

## Adding a new Graph

1. Prepare your knowledge graph (by confinguring your crawlroots and running the crawler and if necessary, making your own edits)
1. Export your graph, giving it a name e.g. `unhealthy-container-apps`

        dotnet run --no-restore --project src/Agent/Agent.Cmd/Agent.Cmd.csproj -- ExportGraphML unhealthy-container-apps
   This will export `unhealthy-container-apps.graphml` to the graphs directory
1. Update `gremlin-server.yaml` and add a new graph entry:

        graphs: {
            ...
            unhealthyContainerApps: /opt/gremlin-server/custom-conf/tinkergraph-stringid.properties,
        }
1. Add code to load.graphgroovy to map the graph unhealthyContainerApps to the file `unhealthy-container-apps.graphml`:

        unhealthyContainerApps.io(GraphMLIo.build()).readGraph('/opt/graphs/unhealthy-container-apps.graphml')
        
1. Add code to load.graphgroovy to set up a traversal source:

        globals << [gunhealthycontainerapps : traversal().withEmbedded(unhealthyContainerApps)]
1. Run the emulator with `run-gremlin-emulator.sh`
1. Check the logs `docker logs gremlin`. You should see a line that looks like:

        [INFO] o.a.t.g.s.u.ServerGremlinExecutor - A GraphTraversalSource is now bound to [gunhealthycontainerapps] with graphtraversalsource[tinkergraph[vertices:XXX edges:XXX], standard]
1. Use the graph in an eval, you can see `MetaAgentFunctionsGraphEvals` as a reference point, note the line `_mocks = new MetaAgentMockSetup(graphName: "gfuncbad");`




