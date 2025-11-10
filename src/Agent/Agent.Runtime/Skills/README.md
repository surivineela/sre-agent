# Skills Directory

This directory contains agent skills - domain-specific knowledge packages that can be dynamically loaded by agents.

## Skills System Overview

Skills provide specialized domain knowledge without requiring separate agents or handoffs. Instead of maintaining a complex agent graph, a single primary agent can load skills on-demand to gain expertise in specific domains.

## Skill Structure

Each skill is a directory containing:

```text
Skills/
└── example_skill/
    ├── metadata.yaml   # Skill metadata (name, description, tools)
    ├── SKILL.md        # Core domain expertise (loaded first)
    ├── examples.md     # Optional: Examples and patterns
    └── reference.md    # Optional: Reference documentation
```

### metadata.yaml

Defines the skill's basic properties:

```yaml
name: example_skill
description: |
  Description of the skill's domain expertise and when it should be used.
  This info will be available in the system context for the agent to see before activating the skill.

# Optional: Tools that should be available when this skill is active
# During migration phase - eventually these will be global
tools:
  - tool_name_1
  - tool_name_2
```

### SKILL.md

Contains the core domain knowledge that gets injected into the agent's context. This is the entrypoint for a skill.

```markdown
# Skill Name

Description of the skill and what it does and can be used for.
Include instructions here for how the skill should be used.

## Capabilities
- What this skill enables
- Key areas of expertise

## How to Use This Skill
- Step-by-step guidance
- Best practices
- Common patterns

## Examples
- Include examples of how to use the skill

## Additional Resources
For detailed info about a scenario you can break info into other markdown files for the agent to dynamically load when needed.
Then just mention those files here, like `scenario.md`.
```

## How Skills Work

### 1. Agent Declares Skills Support

In the agent's YAML configuration:

```yaml
name: meta_agent
system_prompt: |
  You are a meta-agent...

enable_skills: true  # Enables skills for this agent

tools:
  # read_skill_file is automatically added when enable_skills: true
  - other_tool_1
  - other_tool_2
```

### 2. Skills Metadata Injected

When `enable_skills: true`, the agent will have a tool called `read_skill_file` automatically added.
In the tool's description there will be a list of available skills in the <available_skills> tags.

### 3. Model Decides When to Load

The model determines when it needs domain expertise:

```text
User: "Why is my PostgreSQL database slow?"

Model thinks: "This requires PostgreSQL expertise, I should load that skill"

Model calls: read_skill_file(skill_name="postgresql_skill", file_path="SKILL.md")

System returns: [PostgreSQL troubleshooting expertise]

Model uses knowledge to diagnose the issue
```

### 4. Progressive Disclosure

Skills can reference additional files:

```markdown
# In SKILL.md:
For detailed query optimization techniques, read `query-optimization.md`

# Model can then call:
read_skill_file(skill_name="postgresql_skill", file_path="query-optimization.md")
```

## Creating a New Skill

1. **Create skill directory**: `Skills/my_skill/`

2. **Create metadata.yaml**:

    ```yaml
    name: my_skill
    description: Brief description
    tools: []  # Optional
    ```

3. **Create SKILL.md** with domain expertise

4. **Add optional supporting files** as needed

5. **Test**: Enable skills in an agent and verify the skill loads correctly

## Migrating Agents to Skills

To convert an existing agent to a skill:

1. Extract the agent's `system_prompt` → `SKILL.md`
2. Extract `name` and `handoff_description` → `metadata.yaml`
3. Extract `tools` → `metadata.yaml` (temporary, for migration)
4. Break down large prompts into multiple .md files if needed
5. Add sub-agents (from handoffs list) as more .md files if needed
6. Test the skill with an agent that has `enable_skills: true`

A converter utility is provided: `srectl convert-agent --agent <name> --output Skills/`

## Benefits

- **Simplified Architecture**: No agent handoffs, single orchestrator
- **Context Efficiency**: Progressive disclosure loads only what's needed
- **Composable Knowledge**: Skills are modular and reusable
- **Easy Maintenance**: Update skill files independently
- **Model-Driven**: Model decides what expertise to load and when

## Next Steps

1. Create your first skill in this directory
2. Enable skills in meta_agent.yaml: `enable_skills: true`
3. Test skill activation with relevant queries
4. Migrate existing agents incrementally
