# Declarative Evaluations

This directory contains YAML-based declarative evaluation configurations. The test cases in these files will appear in test explorer as variations for `DeclarativeEvalRunner.DeclarativeTest`.

## Quick Start - How do I make a new eval?

1. Prep your knowledge graph for the scenario you want to eval. Its best to clear it out (`g.V().drop()`), set your crawl root and then run the app and let the crawler finish.
1. Export the graph with a name e.g. `just export-graph-to-gremlin container-app-memory-leak`
1. Check the command output, it should have a line like `Traversal source 'gcontainerAppMemoryLeak' created`
1. Run the scenario you want to eval and export the logs with this command:
`
dotnet run --project ./src/Agent/Agent.Cmd/Agent.Cmd.csproj RunScenario 5 "why is my app myapp1 crashing?"
`
1. Find the logs under `ToolReplayLogs/Export/<datetime>`. They are named by thread id. Either read the logs or read the threads in the SRE agent UI and note down which ones are good examples of the agent doing the right thing.
1. Make a `ToolReplayLogs/<name>` folder and copy the good runs there. e.g. `ToolReplayLogs/ContainerAppMemoryLeak`
1. Generate the eval file by having an LLM inspect your logs and infer what good behavior looks like using: `just generate-eval ContainerAppMemoryLeak`
1. Find the generated file `Declarative/ContainerAppMemoryLeak.yml` and update the databse with your traversal source e.g. `database: gcontainerAppMemoryLeak`
1. Important ⚠️ - review the grounded context and example response. These are the crucial part of your eval - another LLM will use these to act as a judge to determine if SRE agent did a good job. Hand edit these as necessary!
1. In VS, build the Agent.Evals project, your eval test cases should appear in the list under `DeclarativeEvalRunner.DeclarativeTest`
1. If they do not appear, run `DeclarativeEvalRunner.ParseYaml` - it should tell you if the yaml has an error
1. You will need to try running the eval and make edits as you discover issues:
    - you might need to add plugin definitions
    - you might need to add fuzzy matching on some tools where the LLM provides slightly different parameters (charts are a great example where it will use a different axis label every time)
1. Consider adding multiple `startMessages` with different phrasing if the system didnt generate them for you - you want multiple datapoints, its hard to assess how the good the eval is from just one run.
1. Keep iterating on the yaml until you feel the eval is successfully measuring the agent's ability to complete a task.

## Tips
Its OK to commit evals that fail often, if you've reviewed them and you think they are demonstrating a problem with SRE agent that we want to fix!

If you are getting lots of "inconclusive" results, its probably because the logs you recorded did not cover enough tool use variations. Record some more logs and dump them in the replay folder your eval is using. You don't need to regenerate the eval yaml file in this case.


## How it works

### Exported Knowledge Graph

These evaluations are designed to run standalone, without access to real resources. To make this possible, they use the gremlin emulator with an exported knowledge graph.

See the gremlin [readme.md](../../../GremlinEmulator/readme.md) for information about exporting a new graph for use in these evals.

For example, to use the knowledge graph setup traversal source "gsimpleWebapps":

```yaml
    configuration:
        database: "gsimpleWebapps"
```

### Tool Replay Logs

Most evals need to do more than just some tool calls against the graph - they need to inspect resources, make changes, etc.

For example, the agent needs to restart a container app as part of fixing a problem - in prod it uses the RestartContainerApp tool to do this, which calls ARM. We want to allow the eval to run without the agent actually restarting a real container app, so we configure it to run from some pre-recorded logs:

```yaml
    toolReplay:
      logDirectory: "ContainerAppsMemoryEvals"
      skipReplayFunctions:
        - "GraphDBPluginDefinition.*"
      fuzzyMatchFunctions:
        - "ChartPluginDefinition.*"
```

In the above example, we load all the logs in `ToolReplayLogs/ContainerAppsMemoryEvals/*` with the following modifications:
- we dont want tool replay for the graphdb tool (because we have an exported graph, see above), so we set it to skip replay.
- the chart tool takes many parameters and its hard to get an exact match every time, so we allow a fuzzy match (i.e. just match on the tool name)

In all other cases, the framework looks for an exact match of the given tool and parameters. If there is no match, the test will terminate with an inconclusive result.

To record these logs see [readme.md](../ToolReplayLogs/readme.md)

## Running Declarative Evaluations

Declarative evaluations are automatically discovered and run by the `DeclarativeEvalRunner` test class. Each YAML file and test case combination becomes an individual test that can be run via:

```bash
# Run all declarative tests
dotnet test ./src/Agent/Agent.Evals/Agent.Evals.csproj --filter DeclarativeTest --no-restore

# Run evals in a given yaml file
dotnet test ./src/Agent/Agent.Evals/Agent.Evals.csproj --filter "Name~SimpleWebApp_VMCount.yaml" --no-restore

# Run a specific test case
dotnet test ./src/Agent/Agent.Evals/Agent.Evals.csproj --filter "Name~SimpleWebApp_VMCount_Case3" --no-restore
```

You can enumerate the declarative tests using --list-tests:

```bash
# List all declarative tests
dotnet test ./src/Agent/Agent.Evals/Agent.Evals.csproj --filter DeclarativeTest --no-restore --list-tests
```

## Examples

For reference implementations, see the existing YAML files in this directory:

- **[SimpleWebApp.yaml](./SimpleWebApp.yaml)** - Basic evaluation testing VM count queries for web apps
- **[ContainerAppsCpuMemory.yaml](./ContainerAppsCpuMemory.yaml)** - Evaluation for diagnosing memory leak issues in container apps, including tool replay configuration




