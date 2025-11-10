# Vanilla Mode Testing Plan

## Overview
This document outlines the comprehensive testing strategy for the vanilla mode feature. Vanilla mode allows agents to run with minimal framework-added instructions, providing a cleaner, more focused agent experience.

## Testing Principles

**⚠️ CRITICAL: Use Existing Test Infrastructure - NO New Mocks**

All tests MUST follow the same integration level as existing tests in their respective test files:

1. **Use Real Components**: Tests should use real `AgentFactory`, `AgentProvider`, `ToolFactory`, etc. - not mocks
2. **Follow Existing Patterns**: Each test file has established patterns for setup and assertions - replicate those patterns exactly
3. **Minimal Mocking**: Only mock what's already mocked in existing tests (e.g., `IChatClient` for LLM calls, HTTP handlers for API tests)
4. **Test Real Code Paths**: The goal is to validate actual framework behavior, not mock behavior
5. **Integration Over Isolation**: These tests should exercise real code integration points to catch actual issues

**Examples by Test File**:
- `AgentFactoryTests.cs` - Uses real factories, loads real YAML files, minimal service mocks
- `AgentProviderTests.cs` - Uses real factories and providers, tests experiment overlays with real agents
- `GeneralAgentEvals.cs` - Full end-to-end tests with real agent runtime (mocks only LLM calls)
- `ExtendedAgentsApiServiceTests.cs` - Uses mock HTTP handlers but real API service logic

**What NOT to do**:
- ❌ Don't create new mocks for `AgentFactory`, `ToolFactory`, or `AgentProvider`
- ❌ Don't mock the vanilla mode logic itself - test the real implementation
- ❌ Don't isolate tests more than existing tests in the same file
- ❌ Don't bypass real YAML loading, agent creation, or instruction building

## Feature Summary
Vanilla mode (`EnableVanillaMode`) is a boolean flag that can be set on agents through:
1. Direct YAML configuration (`vanilla_mode: true`)
2. Experiment overlay (`vanilla_mode: true` in param overlay)
3. API/CLI updates to agent configuration

When enabled, vanilla mode:
- **Disables** handoff instructions
- **Disables** prompt starters
- **Disables** mode configurator prompts
- **Disables** prompt enders
- **Disables** default user message instructions (from ReasoningLoop)
- **Sets** OutputType to `string`
- **Sets** CriticOnHandOff to `false`
- **Sets** MaxReflectionCount to `0`
- **Preserves** ToDo tool (always added)
- **Preserves** agent's own instructions and UserPromptOverride

## Changes Made (Reference)
Key files modified:
- `src/Agent/Agent.Framework/YamlAgentDescriptor.cs` - Added `vanilla_mode` field
- `src/Agent/Agent.Framework/Experiment.cs` - Added `vanilla_mode` to ParamOverlay
- `src/Agent/Agent.Framework/Agent.cs` - Added `ApplyVanillaMode()` method
- `src/Agent/Agent.Framework/AgentFactory.cs` - Skip framework prompts in vanilla mode
- `src/Agent/Agent.Runtime/Reasoning/ReasoningLoop.cs` - Skip default user prompts in vanilla mode
- `src/Agent/Agent.Data/DataModels/AgentDocumentModel.cs` - Added `EnableVanillaMode` field
- `src/Agent/Agent.Web/Views/v2/ExtendedAgentView.cs` - API support for vanilla mode

---

## Test Strategy

> **🔴 CRITICAL REMINDER**:
> - **NO NEW MOCKS** - Use real `AgentFactory`, `AgentProvider`, `ToolFactory` instances
> - **Follow Existing Patterns** - Copy setup from existing tests in each file
> - **Test Real Code** - Validate actual framework behavior, not mock behavior
> - **Same Integration Level** - Match the integration level of existing tests in each file

---

### 1. Unit Tests - Agent Framework
**Location**: `src/Agent/Agent.Tests.Unit/Framework/AgentFactoryTests.cs`

**Integration Level**: Use real `AgentFactory`, `ToolFactory` - same setup as existing tests in this file. Load real YAML files from TestAgents directory.

#### Test 1.1: Agent with Vanilla Mode Enabled via YAML
**Test Name**: `VanillaMode_Agent_SkipsFrameworkInstructions`

**Setup**:
- Create test agent YAML file: `TestAgents/vanilla_agent.yaml`
```yaml
name: vanilla_test_agent
vanilla_mode: true
system_prompt: |
  You are a vanilla agent with minimal instructions.
handoffs:
  - agent2
tools:
  - TestAutoTool
```

**Assertions**:
- Verify `agent.EnableVanillaMode == true`
- Verify `agent.Instructions.ToString()` does NOT contain:
  - Handoff instructions markers
  - Mode configurator additions
  - Prompt ender content
  - Prompt starter content
- Verify `agent.Instructions.ToString()` DOES contain:
  - The agent's own system_prompt
- Verify `agent.OutputType == typeof(string)`
- Verify `agent.CriticOnHandOff == false`
- Verify `agent.MaxReflectionCount == 0`
- Verify ToDo tool is present in agent's tools

#### Test 1.2: Agent with Vanilla Mode Disabled (Default)
**Test Name**: `VanillaMode_Disabled_IncludesFrameworkInstructions`

**Setup**:
- Create test agent YAML file: `TestAgents/non_vanilla_agent.yaml`
```yaml
name: non_vanilla_test_agent
vanilla_mode: false
system_prompt: |
  You are a regular agent.
handoffs:
  - agent2
tools:
  - TestAutoTool
```

**Assertions**:
- Verify `agent.EnableVanillaMode == false`
- Verify `agent.Instructions.ToString()` DOES contain framework additions
- Verify handoff instructions are present
- Verify mode configurator was called

#### Test 1.3: Agent with UserPromptOverride in Vanilla Mode
**Test Name**: `VanillaMode_WithUserPromptOverride_PreservesOverride`

**Setup**:
- Create test agent YAML with both vanilla_mode and user_prompt_override
```yaml
name: vanilla_with_override_agent
vanilla_mode: true
user_prompt_override: "Custom user prompt instructions"
system_prompt: |
  You are a vanilla agent.
```

**Assertions**:
- Verify `agent.EnableVanillaMode == true`
- Verify `agent.UserPromptOverride` is preserved
- Verify agent instructions don't have framework additions
- Verify when `ReasoningLoop.ConstructUserMessage()` is called, it uses the UserPromptOverride

---

### 2. Unit Tests - Experiment Overlay
**Location**: `src/Agent/Agent.Tests.Unit/Framework/AgentProviderTests.cs`

**Integration Level**: Use real `AgentFactory`, `ToolFactory`, and `AgentProvider` - same setup as existing experiment tests (e.g., `AppliesParamOverlayOperations`). Use environment variables to force experiment variants.

#### Test 2.1: Experiment Overlay Enables Vanilla Mode
**Test Name**: `ExperimentOverlay_EnablesVanillaMode_SkipsFrameworkInstructions`

**Setup**:
- Create test experiment YAML: `TestExperiments/VanillaExperiment.yaml`
```yaml
experiment_id: vanilla_experiment
description: Test vanilla mode via overlay
enabled: true
coverage: 1.0
variants:
  - name: vanilla_variant
    split: 1.0
    overlay:
      agent_params:
        - agent_names: ["agent1"]
          vanilla_mode: true
```

**Test Steps**:
1. Set environment variable to force experiment variant: `FrameworkConstants.ForceExperimentVariantsEnvVar = "vanilla_experiment=vanilla_variant"`
2. Create AgentFactory with TestExperiments directory
3. Initialize factory
4. Create AgentProvider with HashVariantAssigner
5. Get agent1 from provider
6. Verify vanilla mode is enabled on the agent

**Assertions**:
- Base agent (without experiment) has `EnableVanillaMode == false` and includes framework instructions
- Agent from provider has `EnableVanillaMode == true`
- Agent instructions do NOT contain framework additions (handoff instructions, prompt starters, etc.)
- Agent has `OutputType == typeof(string)`
- Agent has `CriticOnHandOff == false`
- Agent has `MaxReflectionCount == 0`
- Clean up: Reset environment variable to null

#### Test 2.2: Experiment Overlay with Wildcard Agent Selection
**Test Name**: `ExperimentOverlay_WildcardAgents_AppliesVanillaToAll`

**Setup**:
- Create test experiment YAML with wildcard:
```yaml
experiment_id: vanilla_all_experiment
description: Apply vanilla mode to all agents
enabled: true
coverage: 1.0
variants:
  - name: vanilla_all_variant
    split: 1.0
    overlay:
      agent_params:
        - agent_names: ["*"]
          vanilla_mode: true
```

**Test Steps**:
1. Set environment variable: `FrameworkConstants.ForceExperimentVariantsEnvVar = "vanilla_all_experiment=vanilla_all_variant"`
2. Create AgentFactory and AgentProvider
3. Get multiple agents (agent1, agent2, agent3)
4. Verify all have vanilla mode enabled

**Assertions**:
- All agents have `EnableVanillaMode == true`
- All agents skip framework instructions
- Clean up: Reset environment variable to null

---

### 3. Evaluation Tests - End-to-End Agent Behavior
**Location**: `src/Agent/Agent.Evals/GeneralAgentEvals.cs`

**Integration Level**: Full end-to-end integration - use real `TestHost`, real agents, real `ReasoningLoop`. Only mock LLM calls (IChatClient) as done in existing evals. This tests the complete agent runtime with vanilla mode.

#### Test 3.1: Vanilla Agent in Real Conversation - No Framework User Prompts
**Test Name**: `VanillaAgent_RealChat_NoFrameworkUserPrompts`

**Setup**:
- Create test data file: `Data/VanillaMode/vanilla_basic.json`
```json
{
  "agent": "vanilla_test_agent",
  "messages": [
    {
      "role": "user",
      "content": "What is 2+2?"
    }
  ],
  "expected_tool_calls": [],
  "success_criteria": "Response should be direct answer without extra framework messaging"
}
```
- Create corresponding agent YAML in test setup

**Test Steps**:
1. Initialize test host with vanilla mode agent
2. Start conversation with user message
3. Capture the actual messages sent to the LLM (system + user)
4. Verify no framework additions in user message

**Assertions**:
- User message does NOT contain "Try your best to answer the user's questions"
- User message does NOT contain handoff-related instructions
- User message does NOT contain user question marker (when no override)
- User message IS just the raw user text: "What is 2+2?"
- Agent response is direct and appropriate
- System message (agent instructions) does NOT contain framework prompts

#### Test 3.2: Non-Vanilla Agent in Real Conversation - Includes Framework Prompts
**Test Name**: `NonVanillaAgent_RealChat_IncludesFrameworkUserPrompts`

**Setup**:
- Create test data file: `Data/VanillaMode/non_vanilla_basic.json`
- Use existing agent with vanilla mode disabled

**Test Steps**:
1. Initialize test host with regular agent
2. Start conversation with same user message
3. Capture messages sent to LLM

**Assertions**:
- User message DOES contain "Try your best to answer the user's questions"
- User message DOES contain handoff-related instructions
- User message DOES contain user question marker
- System message contains framework additions (handoff instructions, etc.)

#### Test 3.3: Vanilla Agent with UserPromptOverride
**Test Name**: `VanillaAgent_WithUserPromptOverride_PreservesOverride`

**Setup**:
- Create vanilla agent with `user_prompt_override` set
- Create test data file with user message

**Assertions**:
- User message DOES contain the UserPromptOverride text
- User message DOES contain user question marker (when override is present)
- User message does NOT contain default framework prompts
- Override text is preserved exactly as specified

---

### 4. CLI Tests - API Service
**Location**: `src/Agent/Agent.Cli.UnitTests/Services/API/ExtendedAgentsApiServiceTests.cs`

**Integration Level**: Use real `ApiService` with mocked HTTP handlers - same pattern as existing tests in this file (e.g., `ListAgentsAsync_WithPaginatedResponse_ShouldReturnFormattedAgentList`). Mock only the HTTP transport, not the API service logic.

#### Test 4.1: Apply Agent with Vanilla Mode Returns Correct Configuration
**Test Name**: `ApplyAgentAsync_VanillaMode_ReturnsSuccessAndStoresFlag`

**Setup**:
- Mock HTTP handler to return success response
- Create agent YAML content with `vanilla_mode: true`

**Test Steps**:
1. Call `apiService.ApplyAgentAsync("vanilla_test_agent")`
2. Verify HTTP request was made with correct YAML content
3. Mock successful response

**Assertions**:
- Success is true
- Response message indicates agent applied successfully
- Verify the YAML sent includes `vanilla_mode: true`

#### Test 4.2: Get Agent Configuration Shows Vanilla Mode
**Test Name**: `GetAgentConfigurationAsync_VanillaMode_ReturnsYamlWithFlag`

**Setup**:
- Mock HTTP handler to return agent configuration with vanilla mode enabled
- Create mock response with `enableVanillaMode: true` in JSON

**Test Steps**:
1. Call `apiService.GetAgentConfigurationAsync("vanilla_test_agent")`
2. Parse returned YAML content

**Assertions**:
- Success is true
- Returned YAML content includes `vanilla_mode: true`
- YAML is properly formatted

---

### 5. CLI Integration Tests - srectl Command
**Location**: `src/Agent/Agent.Cli.UnitTests/Commands/` (extend existing command tests or add to ApiServiceTests)

**Integration Level**: Same as section 4 - real API service logic with mocked HTTP handlers. Tests the full YAML parsing and request construction flow.

#### Test 5.1: srectl Apply Reads and Sends Vanilla Mode from YAML
**Test Name**: `Srectl_ApplyAgent_VanillaMode_ParsesYamlCorrectly`

**Setup**:
- Create local agent YAML file: `agents/vanilla_test_agent/vanilla_test_agent.yaml` with `vanilla_mode: true`
- Mock HTTP responses for apply operation

**Test Steps**:
1. Execute apply logic (call ApiService.ApplyAgentAsync)
2. Verify YAML was read correctly
3. Verify vanilla_mode flag was included in the request

**Assertions**:
- Apply succeeds
- HTTP request contains vanilla_mode: true in YAML body
- Success message returned

#### Test 5.2: srectl Get Agent Displays Vanilla Mode
**Test Name**: `Srectl_GetAgent_VanillaMode_DisplaysInYaml`

**Setup**:
- Mock GetAgentConfigurationAsync to return YAML with vanilla_mode: true

**Test Steps**:
1. Call GetAgentConfigurationAsync
2. Verify returned YAML content

**Assertions**:
- Returned YAML includes `vanilla_mode: true`
- YAML is properly formatted
- No errors during retrieval

---

## Test Data Files to Create

### Agent YAML Files
**Location**: `src/Agent/Agent.Tests.Unit/Framework/TestAgents/`

1. **vanilla_agent.yaml**
```yaml
name: vanilla_test_agent
vanilla_mode: true
system_prompt: |
  You are a vanilla agent with minimal instructions.
  Answer questions directly and concisely.
handoffs:
  - agent2
tools:
  - TestAutoTool
common_prompts: []
```

2. **vanilla_with_override.yaml**
```yaml
name: vanilla_with_override_agent
vanilla_mode: true
user_prompt_override: |
  Custom user instructions here.
  This should be preserved.
system_prompt: |
  You are a vanilla agent with user prompt override.
tools:
  - TestAutoTool
```

3. **vanilla_with_common.yaml**
```yaml
name: vanilla_with_common_agent
vanilla_mode: true
system_prompt: |
  You are a vanilla agent.
common_prompts:
  - prompt1
tools:
  - TestAutoTool
```

### Experiment YAML Files
**Location**: `src/Agent/Agent.Tests.Unit/Framework/TestExperiments/`

1. **VanillaExperiment.yaml**
```yaml
experiment_id: vanilla_experiment
description: Test vanilla mode overlay on specific agent
enabled: true
coverage: 1.0
variants:
  - name: vanilla_variant
    split: 1.0
    overlay:
      agent_params:
        - agent_names: ["agent1"]
          vanilla_mode: true
```

2. **VanillaAllExperiment.yaml**
```yaml
experiment_id: vanilla_all_experiment
description: Apply vanilla mode to all agents via wildcard
enabled: true
coverage: 1.0
variants:
  - name: vanilla_all_variant
    split: 1.0
    overlay:
      agent_params:
        - agent_names: ["*"]
          vanilla_mode: true
```

3. **VanillaWithOtherParams.yaml**
```yaml
experiment_id: vanilla_combined_experiment
description: Vanilla mode combined with other parameters
enabled: true
coverage: 1.0
variants:
  - name: vanilla_combined_variant
    split: 1.0
    overlay:
      agent_params:
        - agent_names: ["agent1"]
          vanilla_mode: true
          temperature: 0.7
          model_name: gpt-4o
```

### Evaluation Test Data Files
**Location**: `src/Agent/Agent.Evals/Data/VanillaMode/`

1. **vanilla_basic.json**
```json
{
  "agent": "vanilla_test_agent",
  "messages": [
    {
      "role": "user",
      "content": "What is 2+2?"
    }
  ],
  "expected_behavior": {
    "user_message_should_not_contain": [
      "Try your best to answer the user's questions",
      "If you find a suitable agent to handoff to",
      "HandoffBack"
    ],
    "user_message_should_be_raw": true
  }
}
```

2. **non_vanilla_basic.json**
```json
{
  "agent": "agent1",
  "messages": [
    {
      "role": "user",
      "content": "What is 2+2?"
    }
  ],
  "expected_behavior": {
    "user_message_should_contain": [
      "Try your best to answer the user's questions"
    ]
  }
}
```

3. **vanilla_with_override.json**
```json
{
  "agent": "vanilla_with_override_agent",
  "messages": [
    {
      "role": "user",
      "content": "Help me debug this issue"
    }
  ],
  "expected_behavior": {
    "user_message_should_contain": [
      "Custom user instructions here"
    ],
    "user_message_should_not_contain": [
      "Try your best to answer the user's questions"
    ]
  }
}
```

---

## Test File Locations Summary

### Tests to Add to Existing Files

1. **AgentFactoryTests.cs**
   - **Location**: `src/Agent/Agent.Tests.Unit/Framework/AgentFactoryTests.cs`
   - **Tests to Add**:
     - Test 1.1: `VanillaMode_Agent_SkipsFrameworkInstructions`
     - Test 1.2: `VanillaMode_Disabled_IncludesFrameworkInstructions`
     - Test 1.3: `VanillaMode_WithUserPromptOverride_PreservesOverride`

2. **AgentProviderTests.cs**
   - **Location**: `src/Agent/Agent.Tests.Unit/Framework/AgentProviderTests.cs`
   - **Tests to Add**:
     - Test 2.1: `ExperimentOverlay_EnablesVanillaMode_SkipsFrameworkInstructions`
     - Test 2.2: `ExperimentOverlay_WildcardAgents_AppliesVanillaToAll`
   - **Note**: This file contains all the existing experiment overlay tests (prompt, tool, handoff, param overlays)

3. **GeneralAgentEvals.cs**
   - **Location**: `src/Agent/Agent.Evals/GeneralAgentEvals.cs`
   - **Tests to Add**:
     - Test 3.1: `VanillaAgent_RealChat_NoFrameworkUserPrompts`
     - Test 3.2: `NonVanillaAgent_RealChat_IncludesFrameworkUserPrompts`
     - Test 3.3: `VanillaAgent_WithUserPromptOverride_PreservesOverride`
   - **Note**: These will use the GeneralAgentEvals infrastructure that loads test cases from JSON files

4. **ExtendedAgentsApiServiceTests.cs** (partial class ApiServiceTests)
   - **Location**: `src/Agent/Agent.Cli.UnitTests/Services/API/ExtendedAgentsApiServiceTests.cs`
   - **Tests to Add**:
     - Test 4.1: `ApplyAgentAsync_VanillaMode_ReturnsSuccessAndStoresFlag`
     - Test 4.2: `GetAgentConfigurationAsync_VanillaMode_ReturnsYamlWithFlag`

5. **CLI Command Tests** (extend existing or add to ApiServiceTests)
   - **Location**: `src/Agent/Agent.Cli.UnitTests/Services/API/ExtendedAgentsApiServiceTests.cs` or relevant command test file
   - **Tests to Add**:
     - Test 5.1: `Srectl_ApplyAgent_VanillaMode_ParsesYamlCorrectly`
     - Test 5.2: `Srectl_GetAgent_VanillaMode_DisplaysInYaml`

---

## Test Execution Strategy

### Phase 1: Unit Tests (Priority: High)
Execute tests 1.1-1.3 in AgentFactoryTests and 2.1-2.2 in AgentProviderTests. These validate the core functionality at the framework level.

**Command**:
```bash
dotnet test src/Agent/Agent.Tests.Unit/Agent.Tests.Unit.csproj --filter "FullyQualifiedName~VanillaMode|FullyQualifiedName~ExperimentOverlay"
```

### Phase 2: Evaluation Tests (Priority: High)
Execute tests 3.1-3.3 in GeneralAgentEvals to verify end-to-end behavior with real conversations.

**Command**:
```bash
dotnet test src/Agent/Agent.Evals/Agent.Evals.csproj --filter "FullyQualifiedName~VanillaAgent"
```
Or run specific test data:
```bash
$env:TEST_FOLDER="Data/VanillaMode"; dotnet test src/Agent/Agent.Evals/Agent.Evals.csproj
```

### Phase 3: CLI/API Tests (Priority: Medium)
Execute tests 4.1-4.2 and 5.1-5.2 to verify CLI/API layer.

**Command**:
```bash
dotnet test src/Agent/Agent.Cli.UnitTests/Agent.Cli.UnitTests.csproj --filter "FullyQualifiedName~Vanilla"
```

---

## Validation Checklist

For each test scenario, verify:

### ✅ Vanilla Mode Enabled
- [ ] `agent.EnableVanillaMode == true`
- [ ] `agent.OutputType == typeof(string)`
- [ ] `agent.CriticOnHandOff == false`
- [ ] `agent.MaxReflectionCount == 0`
- [ ] Agent instructions do NOT contain handoff instructions
- [ ] Agent instructions do NOT contain prompt starters
- [ ] Agent instructions do NOT contain prompt enders
- [ ] Agent instructions do NOT contain mode configurator additions
- [ ] User message construction does NOT add default instructions
- [ ] ToDo tool IS present (always added)
- [ ] Agent's own system_prompt IS preserved
- [ ] UserPromptOverride IS preserved (if set)
- [ ] Common prompts ARE added (if explicitly listed)

### ✅ Vanilla Mode Disabled (Control)
- [ ] `agent.EnableVanillaMode == false`
- [ ] Agent instructions DO contain framework additions
- [ ] User message construction DOES add default instructions
- [ ] Handoff instructions are present
- [ ] Mode configurator was invoked

### ✅ API/Persistence
- [ ] Vanilla mode flag is stored in database
- [ ] Vanilla mode flag is returned in API responses
- [ ] Vanilla mode flag can be updated via PATCH
- [ ] Vanilla mode flag can be set via YAML apply

### ✅ Experiments
- [ ] Experiment overlay can enable vanilla mode
- [ ] Experiment overlay applies to specific agents
- [ ] Experiment overlay applies to wildcard agents
- [ ] Experiment overlay combines with other parameters

---

## Edge Cases to Test

1. **Vanilla Mode + Handoff Instructions Override**
   - If agent has `handoff_prompt_override` set, does vanilla mode respect it or ignore it?
   - Expected: Should be ignored (vanilla mode takes precedence)

2. **Vanilla Mode + Instructions Override**
   - If agent has `instructions_override` set, does vanilla mode apply?
   - Expected: Override should still work (it's explicit)

3. **Vanilla Mode + Max Reflection Count > 0**
   - If YAML sets both `vanilla_mode: true` and `max_reflection_count: 3`, which wins?
   - Expected: Vanilla mode wins (sets to 0)

4. **Vanilla Mode + OutputType Specified**
   - If YAML sets both `vanilla_mode: true` and `output_type: MyCustomType`, which wins?
   - Expected: Vanilla mode wins (sets to string)

5. **Experiment Toggle**
   - If agent starts with vanilla mode, then experiment disables it (sets to false), does it work?
   - Expected: Should work (overlay can toggle both ways)

---

## Success Criteria

### Must Pass
- All unit tests in AgentFactoryTests (1.1-1.3)
- All experiment overlay tests in AgentProviderTests (2.1-2.2)
- Evaluation tests in GeneralAgentEvals (3.1-3.3) - validates real conversation behavior
- At least one CLI/API test (4.1 or 4.2)

### Should Pass
- All CLI/API tests (4.1-4.2, 5.1-5.2)
- Edge case tests

### Performance
- Vanilla mode should not impact agent initialization time
- Vanilla mode should reduce prompt token count (less instructions = fewer tokens sent to LLM)

---

## Notes

### Key Behaviors to Validate
1. **ToDo Tool Always Added**: Even in vanilla mode, the ToDo tool common prompt is added. This is intentional and should be verified.

2. **Common Prompts Still Work**: If an agent explicitly lists common_prompts, those should still be added even in vanilla mode. Vanilla mode only skips *framework-automatic* additions.

3. **UserPromptOverride Preserved**: Vanilla mode should not interfere with explicit user prompt overrides.

4. **Experiment Overlay Priority**: When vanilla mode is set via experiment overlay, it should override the base agent configuration.

### Testing Tools Available
- `AgentFactory<AgentContext>` - For loading agents and experiments (used in unit tests) - **USE REAL INSTANCE**
- `AgentProvider<AgentContext>` - For applying experiment overlays - **USE REAL INSTANCE**
- `ToolFactory<AgentContext>` - For tool setup - **USE REAL INSTANCE**
- `GeneralAgentEvals` infrastructure - For end-to-end conversation testing with real LLM interactions
- Mock HTTP handlers in `ApiServiceTests` - For CLI/API testing without actual network calls
- Test data files (JSON/YAML) - For test configuration

### Implementation Guidelines

**DO**:
- ✅ Follow existing test patterns in each file exactly
- ✅ Use real factory and provider instances
- ✅ Load real YAML configuration files
- ✅ Test actual instruction building and overlay application
- ✅ Verify real agent properties and behavior
- ✅ Use existing mock infrastructure (IChatClient, HTTP handlers)

**DON'T**:
- ❌ Create new mocks for framework components (factories, providers, agents)
- ❌ Mock vanilla mode logic - test the real implementation
- ❌ Isolate tests beyond what existing tests do
- ❌ Skip real YAML parsing or agent initialization
- ❌ Mock method return values that should come from real code execution

**Reference Existing Tests**:
- Study `AppliesParamOverlayOperations()` in AgentProviderTests for experiment pattern
- Study `ListAgentsAsync_WithPaginatedResponse_ShouldReturnFormattedAgentList()` for API service pattern
- Study existing GeneralAgentEvals test cases for eval pattern
- Copy setup code from existing tests rather than creating new mocks

### Documentation References
- Vanilla mode implementation: See git commits in `vanilla` branch
- Experiment system: `docs/experiments.md`
- Agent factory: `src/Agent/Agent.Framework/AgentFactory.cs`
- Reasoning loop user message construction: `src/Agent/Agent.Runtime/Reasoning/ReasoningLoop.cs` (ConstructUserMessage method)
