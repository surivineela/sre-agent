# Writing Evals for Agents

You can start writing tests to verify the inputs and outputs of the LLM model (model generation), as well as tests for the function calls made by the LLM. There are some existing tests you can use as examples to get started with writing tests for your scenario.

## Get the Required Data

First, you’ll need to run your agent and get the trace data. You can retrieve this in multiple ways. The fastest method most folks use is running the debug app locally, then going to `localhost:3000` to grab the trace data.  
For more details, see: [Debugging Agent Threads Locally](https://github.com/serverless-paas-balam/sreagent-runtime/wiki/Debugging-Agent-Threads-Locally)

If you want to test the input/output or chat thread of your agents running in production, refer to:  
[Agent Trace Debugging](https://github.com/serverless-paas-balam/sreagent-runtime/wiki/Agent-Trace-Debugging)

**Tip:** If you’re testing your model’s generation (i.e., when the model receives user input along with a system prompt and decides to make a tool call or output text), you’ll benefit from using the `model.generation` part of the trace rather than downloading the entire trace. You can use the last `model.generation` step, since the chat history gets appended and contains all decisions.
![alt text](image.png)

You can download the trace using the following download button as well
![alt text](image-1.png)

## Example Methods

Here are a few example tests that may help you get started with writing and testing your agents. There are more examples in the same `Agent.Evals` project:

- [GeneralAgentTests](https://github.com/serverless-paas-balam/sreagent-runtime/blob/main/src/Agent/Agent.Evals/GeneralAgentEvals.cs):  
  `GeneralAgentTests_DetailedComparison` — This method does a detailed comparison and logs whether the results were as expected, rather than asserting.

- [TrajectoryEvals](https://github.com/serverless-paas-balam/sreagent-runtime/blob/main/src/Agent/Agent.Evals/TrajectoryEvals.cs):  
  `DebugTraceQuality_EvaluateResponses` — This method gets the chat trajectories and ensures that after the LLM reasoning is complete, the trajectory is at the intended step, and asserts this.


## Methods that can be used to deserialize the data
- [LoadChatMessagesFromDebuggerTraces](https://github.com/serverless-paas-balam/sreagent-runtime/blob/856b6d85aeedf4cf323d02bca92599b2c639de51/src/Agent/Agent.Evals/ModelGenerationDataLoader.cs#L58): The traces that you download can be given to this method to deserialize it and return a dictionary with file names as keys and ChatMessages as values.
- [LoadChatMessagesFromJsonFiles](https://github.com/serverless-paas-balam/sreagent-runtime/blob/856b6d85aeedf4cf323d02bca92599b2c639de51/src/Agent/Agent.Evals/ModelGenerationDataLoader.cs#L15): This method will help you deserialize the json retrieved from the model generation step from the debugger tool.