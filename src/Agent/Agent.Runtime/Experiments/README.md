# Experiments

This folder contains experiment configuration files for A/B testing agent configurations. Experiments allow you to define multiple variants of an agent setup and control how traffic is split between them.

## Schema Reference

Experiment files are defined in YAML format. See `Agent.Framework/Experiment.cs` for the full schema definition.

### Top-Level Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `experiment_id` | string | Yes | - | Unique identifier for the experiment |
| `description` | string | No | `""` | Human-readable description |
| `enabled` | bool | No | `true` | Whether the experiment is active |
| `unit` | enum | No | `Global` | Unit of randomization: `Global` (per instance) or `PerThread` (per thread) |
| `coverage` | double | No | `1.0` | Fraction of traffic to include (0.0 to 1.0) |
| `variants` | list | Yes | - | List of variant definitions |

### Variant Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Unique name for this variant |
| `split` | double | Yes | Fraction of experiment traffic for this variant (0.0 to 1.0) |
| `overlay` | object | No | Configuration overrides to apply |

### Overlay Types

#### `prompts` - Prompt Overrides

| Field | Description |
|-------|-------------|
| `agent_names` | List of agent names to apply changes to |
| `replace_system_prompt` | Completely replace the system prompt |
| `append_system_prompt` | Text to append to existing prompt |
| `prepend_system_prompt` | Text to prepend to existing prompt |
| `handoff_instructions` | Override handoff instructions |
| `common_prompts` | List of common prompt names to include |
| `has_handoff_instructions` | Include standard handoff instructions (default: true) |
| `apply_standard_modifiers` | Apply standard prompt modifiers (default: true) |
| `enable_vanilla_mode` | Remove all prompt modifiers (default: false) |
| `user_prompt_override` | Override user prompt |

#### `tools` - Tool Overrides

| Field | Description |
|-------|-------------|
| `agent_names` | List of agent names to apply changes to |
| `replace_tools` | Replace all tools with this list |
| `add_tools` | Add these tools to existing set |
| `remove_tools` | Remove these tools from existing set |

#### `handoffs` - Handoff Overrides

| Field | Description |
|-------|-------------|
| `agent_names` | List of agent names to apply changes to |
| `replace_handoffs` | Replace all handoffs with this list |
| `add_handoffs` | Add these handoffs to existing set |
| `remove_handoffs` | Remove these handoffs from existing set |

#### `agent_params` - Agent Parameter Overrides

| Field | Description |
|-------|-------------|
| `agent_names` | List of agent names to apply changes to |
| `model_name` | Override the model name |
| `reasoning_effort_level` | Set reasoning effort level |
| `output_type` | Set output type (e.g., `string`) |
| `enable_skills` | Enable/disable skills |
| `add_system_skills` | Add system skills |
| `allow_parallel_tool_calls` | Allow parallel tool calls |

#### `system` - System Overrides (Global experiments only)

| Field | Description |
|-------|-------------|
| `feature_flags` | List of feature flags to enable |

## Sample Experiment

```yaml
experiment_id: my_experiment
description: Example experiment demonstrating A/B testing of agent configurations.
enabled: true
unit: PerThread
coverage: 0.2  # 20% of threads participate in this experiment

variants:
  - name: variant1
    split: 0.5  # 50% of experiment traffic
    overlay:
      prompts:
        - agent_names:
            - my_agent
          append_system_prompt: |
            Additional instructions for variant1 group.

  - name: varaint2
    split: 0.5  # 50% of experiment traffic
    overlay:
      prompts:
        - agent_names:
            - my_agent
          replace_system_prompt: |
            Completely new instructions for variant2 group.
          apply_standard_modifiers: false
          enable_vanilla_mode: true
      tools:
        - agent_names:
            - my_agent
          replace_tools:
            - SearchResource
            - GetResourceProperties
            - RunDiagnostics
      handoffs:
        - agent_names:
            - my_agent
          replace_handoffs: []  # Remove all handoffs
      agent_params:
        - agent_names:
            - my_agent
          enable_skills: true
          allow_parallel_tool_calls: true
          output_type: string
```

## Validation Rules

- Experiments must have at least one variant
- Total variant splits must be greater than zero (splits are normalized if they don't sum to 1.0)
- Individual variant splits must be between 0 and 1
- System overlay feature flags are only supported for `Global` experiments
