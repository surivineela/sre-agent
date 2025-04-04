# How to add a Sub-Agent to the System
For the purposes of this doc, we are adding a sub-agent called `MyAgent`.

It's recommended you use the existing sub-agents as demonstrations, using the docs below as more of a checklist.

## Steps
- In `Agent.Runtime`
	- Under `SubAgents` Create a folder `MyAgent`.
		- Add a class `MyAgent` in the folder.
		  - It derives from `GenericAgentOrchestrator<MyAgentInput, string>`
		  - It has attribute `[DurableTask]`
		- Create the prompt for your agent: A `txt` file in the same directory which can be loaded later.
		- Create a planning activity for the agent
		  - It derives from `TaskActivity<MyAgentPlanInput, List<ChatMessage>>`
		  - It reads the prompt text created above
		- Create factory class `MyAgentFactory`
		  - This is where you load all the tools your prompt will need, and where you can kick off the agent orchestration
	- In `MetaAgent`
		- Under `SubAgentPlugins` create a class `MyAgentPlugin`
		- Modify `MetaAgent.cs` to initialize your new plugin same as the others, and register it in `_aiTools`
- In `Agent.Web`
	- `AddTransient<MyAgentPlugin>`
	- `AddSingleton<MyAgentFactory>`
- If you are adding totally new capabilities to the system, you will likely add them under `Agent.Plugins` under the `Definitions` and `Implementations` folders.
