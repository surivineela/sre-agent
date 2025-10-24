# Agent Experiment System

## Overview

The Agent Experiment System enables controlled A/B testing of agent behavior at runtime. It allows you to modify agent properties (prompts, tools, handoffs) for specific experiment variants and automatically assign different configurations to threads or instances.

---

## Architecture

### High-Level Flow

```text
┌─────────────────────────────────────────────────────────────┐
│                        User Request                         │
│                      (with threadId)                        │
└─────────────────────────┬───────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                    AgentProvider                            │
│  • Singleton service managing variant assignment            │
│  • Caches agent graphs by variant combination               │
│  • Clones base agents and applies overlays                  │
└────────────────┬────────────────────────┬───────────────────┘
                 │                        │
                 ▼                        ▼
        ┌─────────────────┐       ┌───────────────┐
        │ VariantAssigner │       │ AgentFactory  │
        │ (hash-based)    │       │ (base agents) │
        └─────────────────┘       └───────────────┘
                 │
                 ▼
        ┌────────────────────────────────────┐
        │  Deterministic variant assignment  │
        │  based on experimentId + unitKey   │
        └────────────────────────────────────┘
```

### Component Overview

#### AgentProvider**

The central component that manages per-thread agent retrieval with experiments applied:

- **Variant Assignment**: Determines which experiment variant applies to each thread/instance
- **Agent Graph Caching**: Caches cloned agent graphs by variant combination (not per thread)
- **Graph Cloning**: Clones base agent graph while preserving handoff relationships
- **Overlay Application**: Applies experiment overlays to cloned graphs

#### **VariantAssigner**

Provides deterministic hash-based variant assignment:

- Uses SHA256 hash of `experimentId + unitKey` (where unitKey is instanceId or threadId)
- Maps hash value to variant based on configured weights
- Same input always produces same variant assignment

#### **Experiment Configuration**

Loaded from YAML files, defining:

- Experiment metadata (ID, description, enabled state)
- Assignment unit (Global or PerThread)
- Variants with weights and overlays
- Coverage percentage (future: rollout control)

---

## How It Works

### 1. Experiment Definition

Experiments are defined in YAML files with this structure:

```yaml
experiment_id: improved_prompt_experiment
description: Test improved system prompt for better reasoning
enabled: true
unit: PerThread  # or Global
coverage: 0.5    # 50% of threads will be included in this experiment
variants:
- name: treatment1
  split: 0.5     # 50% of threads in the experiment have treatment1 applied
  overlay:
    prompts:
      - agent_names:
          - meta_agent
          - rca_agent
        replace_system_prompt: |
          [New improved prompt text here...]

    tools:
      - agent_names:
          - meta_agent
        replace_tools:
          - tool1
          - tool2
          - new_tool3

    handoffs:
      - agent_names:
          - meta_agent
        replace_handoffs:
          - specialist_agent
- name: treatment2
  split: 0.5     # 50% of threads in the experiment have treatment2 applied
  overlay:
    prompts:
      - agent_names:
          - meta_agent
          - rca_agent
        append_system_prompt: |
          [Instead of replacing the whole prompt we can also just append to it...]

    tools:
      - agent_names:
          - meta_agent
        add_tools:
          - new_tool3
          - new_tool4

    handoffs:
      - agent_names:
          - meta_agent
        remove_handoffs:
          - dont_handoff_to_this_agent
```

### 2. Variant Assignment

When a request arrives with a threadId:

**For Global experiments** (unit: Global):

- Hash(`experimentId` + `instanceId`) → variant
- All threads on the same instance get the same variant

**For PerThread experiments** (unit: PerThread):

- Hash(`experimentId` + `threadId`) → variant
- Each thread can get a different variant

The hash ensures:

- **Deterministic**: Same thread always gets same variant
- **Distributed**: Variants are evenly distributed based on weights
- **Stable**: Variant doesn't change between requests

### 3. Agent Graph Caching

The system caches agent graphs by **variant combination**, not by thread:

**Example:**

```text
Thread A: {exp1: control, exp2: treatment} → Cache Key 1
Thread B: {exp1: control, exp2: treatment} → Cache Key 1 ✓ (reused)
Thread C: {exp1: treatment, exp2: control} → Cache Key 2
```

This means:

- First request with a variant combination clones the graph
- Subsequent requests with same combination reuse cached graph
- Efficient memory usage even with multiple threads

### 4. Graph Cloning Process

When creating a new agent graph for a variant combination:

1. **Clone all base agents** (shallow copy of properties)
2. **Rebuild handoff references** pointing to cloned agents (preserves graph structure)
3. **Apply variant overlays** from all active experiments
4. **Cache the result** keyed by variant combination

---

## Overlay Types

### Prompt Overrides

Alter the system prompt for specific agents:

```yaml
prompts:
  - agent_names: # one more agent names, '*' to apply to all agents
      - agent1
      - agent2
    replace_system_prompt: |
      New system prompt here.
      Can be multi-line.
    append_system_prompt: |
      Add to existing system prompt
    prepend_system_prompt: |
      Add prefix to existing system prompt
    handoff_instructions: |
      Change the handoff instructions for an agent

    # Names of common prompts to add to the agent's prompt template.
    # If `apply_standard_modifiers` is true, these will be added in addition to the common prompts configured on the base agent.
    # Otherwise, these will replace the base agent's common prompts.
    common_prompts:
      - prompt1
      - prompt2

    # Whether the standard handoff instructions should be included. True by default.
    # If `apply_standard_modifiers` is true, this value will be ignored.
    # Only applies if `replace_system_prompt` is set.
    has_handoff_instructions: false

    # If true, applies the standard prompt modifiers in `AgentFactory.ConfigureAgentInstructions()`, which includes
    # adding handoff instructions, runtime-configured prompt starters/enders, and the common prompts configured on the base agent.
    # Only applies if `replace_system_prompt` is set.
    apply_standard_modifiers: false
```

### Tool Overrides

Alter the tool list for specific agents:

```yaml
tools:
  - agent_names:
      - agent1
    replace_tools:
      - tool1
      - tool2
      - tool3
    add_tools:
      - new_tool
    remove_tools:
      - tool_to_remove
```

### Handoff Overrides

Change the list of agents that can be handed off to:

```yaml
handoffs:
  - agent_names:
      - agent1
    replace_handoffs:
      - specialist_agent
      - another_agent
    add_handoffs:
      - new_agent
    remove_handoffs:
      - agent_to_remove
```

### Parameter Overrides

Change the model or framework parameters used by agents:

```yaml
agent_params:
  - agent_names:
      - agent1
    model_name: gpt-5
    reasoning_effort_level: medium
    output_type: string # or other structured type defined in the runtime assembly
```

---

## Experiment Units

### Global (unit: Global)

All threads in the same instance get the same variant.

**Assignment key:** Instance ID (from `AGENT_NAME` environment variable)

### PerThread (unit: PerThread)

Each thread can get a different variant independently.

**Assignment key:** Thread ID

---

## Debugging with Forced Variants

Override variant assignment for testing using the `FORCE_EXPERIMENT_VARIANTS` environment variable:

```bash
# Force specific variants (semicolon-separated)
export FORCE_EXPERIMENT_VARIANTS="experiment1=treatment;experiment2=control"

# Run the application
dotnet run
```

This forces all threads to use the specified variants regardless of hash assignment.

**Use cases:**

- Local development testing
- Validating both variants work correctly
- Reproducing specific variant behavior
- Integration testing

---

## Example: Running an Experiment

### 1. Create Experiment File

`src/Agent/Agent.Runtime/Experiments/MetaAgentPrompt.yaml`:

```yaml
experiment_id: meta_agent_reasoning_v2
description: Improved reasoning prompt for meta agent
enabled: true
unit: PerThread
coverage: 0.2 # 20% of threads covered
variants:
- name: improved
  split: 1.0 # only 1 variation being tested
  overlay:
    prompts:
      - agent_names:
          - meta_agent
        replace_system_prompt: |
          You are an expert SRE assistant.

          When analyzing issues:
          1. Gather context systematically
          2. Form testable hypotheses
          3. Validate before concluding

          [rest of prompt...]
```

### 2. Deploy and Analyze results

The experiment is automatically loaded when the application starts, check telemetry for threads using the experiment variant.

### 3. Graduate or Rollback

**If treatment wins:**

Apply changes directly to agent yaml files, remove experiment file.

**If control is better:**

Change the experiment values to test another change. Or delete / disable the experiment file so it is not applied anymore.

---

## Related Files

- **Experiment definitions**: `src/Agent/Agent.Runtime/Experiments/*.yaml`
- **Core implementation**: `src/Agent/Agent.Framework/`
  - `Experiment.cs` - Data models
  - `AgentProvider.cs` - Assignment and caching
  - `VariantAssigner.cs` - Hash-based assignment
  - `AgentGraphCloner.cs` - Graph cloning logic
- **Usage**: `src/Agent/Agent.Runtime/Reasoning/`
  - `ReasoningLoop.cs`
  - `WorkflowOrchestrator.cs`
