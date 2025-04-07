# How to add a Sub-Agent to the System
There's a standard that several agents inherit from; these are agents that take action on specific resource types (storage accounts, cosmosDb, etc) - but fundamentally they are similar in that they are given a list of resources, and [optional] actions to take, and they perform those actions.

Optionally, some of them are "scanners" in that they ping users and warn them about changes that should be made to their resources (say, security issues).

If your agent is of this type, you can get away with writing *very* little code by inheriting in the same way as the rest. You can use the `StorageAccountAgent` as a sample, but most of your code will be guided by the type system: You only need to add the code that the compiler forces you to.

For the purposes of this doc, we are adding a sub-agent called `MyAgent`.


## Steps
- In `Agent.Runtime`
  - Under `SubAgents` Create a folder `MyAgent`.
     - Define inputs for the agent and the planning activity:
        - Add a record `MyAgentActivityInput` that derives from `SimpleResourceSubAgentInput`. When your action workflow runs, this is the input it needs to take action.
        - Add a record `MyAgentInput` that derives from `SimpleResourceSubAgentInput<MyAgentActivityInput>`; this is the overall input sent to the agent each time, telling it which conversation to deal with, and which tools to use.
	  - Create the activity for the agent `MyAgentActivity`: This is what defines the brains of the agent
		   - It derives from `SimpleResourceSubAgentActivity<MyAgentPlanInput>`
		   - It has attribute `[DurableTask]`
	  - Add a class `MyAgent` in the folder.
		   - It derives from `SimpleResourceSubAgentBase<MyAgentInput, MyAgentActivity, MyAgentActivityInput>`
		   - It has attribute `[DurableTask]`
	  - Create factory class `MyAgentFactory`
		   - It derives from `SimpleResourceSubAgentFactoryBase<MyAgent, MyAgentInput, MyAgenActivity, MyAgenActivityActivityInput>`
       - This is where you load all the tools your prompt will need, and where you kick off the agent orchestration
    - [OPTIONAL] If your agent needs to be proactive (scan resources and suggest changes), then:
       - Create class `MyAgentScanner`
          - It derives from `SimpleResourceSubAgentScannerBase<MyAgent, MyAgentInput, MyAgenActivity, MyAgenActivityActivityInput>`
  - Under `MetaAgent`
      - Under `SubAgentPlugins` create a class `MyAgentPlugin`
        - It derives from `SimpleResourceSubAgentPluginBase<MyAgentFactory, MyAgent, MyAgentInput, MyAgenActivity, MyAgenActivityActivityInput>`
        - The two methods you implement here should just call their subclass implementations, but
        you must tag them with the `[Description]` and `[KernelFunction]` attributes, providing descriptions
        for the AI to interpret.
      - In `MetaAgent.cs`, modify the main prompt to tell it about your agent.
- If you are adding totally new capabilities to the system, you will likely add them under `Agent.Plugins` under the `Definitions` and `Implementations` folders.
