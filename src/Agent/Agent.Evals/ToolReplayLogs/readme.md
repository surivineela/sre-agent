# Tool Replay Logs

This folder contains recorded chat history for agents performing tasks. These logs can be used to run evals that require steps such making ARM calls, kubectl calls or Azure CLI calls without actually making those calls against real resources.

For an example, see `ContainerAppsCpuMemoryScenarios1Evals`.

## Format

The log format is just a JSON serialized list of ChatMessages from the agent reasoning loop. Take note of how the property names were serialized - depending on how you captured this, you might need to pass JSON serializer options that specify the right pascal/camel case naming.

If you record the logs using the step described below, then you shouldn't need to specify any special serializer settings - `RegisterServicesForAgentFrameworkEval` will use the correct settings.

## Recording Logs

The easiest way to record these logs is to use the new command in Agent.Cmd. For example, to start 10 copies of the agent with the message "fix my app foo":

```
just run-cmd RunScenario 10 "fix my app foo"
```

This command runs against `http://localhost:5073` by default, but you can pass a different endpoint with `--url`

It will automatically create a json file for each thread ID in `ToolReplayLogs/Export/<timestamp>`. Once you have the logs you want you should move them into the folder structure with a clear name. Please don't commit them in the "export" folder, it will become a huge mess.
