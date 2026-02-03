# Agent Hooks System

Hooks allow you to intercept and control agent behavior at specific points during execution. You can use hooks to validate agent responses, audit tool usage, inject additional context, or enforce custom policies.

## Hook Events

### Stop Hook

**Triggered when:** The agent is about to complete and return a final response.

**Use cases:**
- Validate that the agent's response meets quality criteria
- Ensure required information is included in the response
- Force the agent to continue working if the task isn't complete

**Behavior:**
- If the hook **allows** (returns `ok: true`), the agent stops and returns its response
- If the hook **rejects** (returns `ok: false` with a `reason`), the reason is injected as a user message and the agent continues working
- Prompt-type Stop hooks have a `maxRejections` limit (default: 3) to prevent infinite loops

### PostToolUse Hook

**Triggered when:** A tool has finished executing, before the result is added to the conversation.

**Use cases:**
- Audit tool usage (log what tools are being called)
- Validate tool results before the agent sees them
- Inject additional context based on tool output
- Block certain tool results from being used

**Behavior:**
- If the hook **allows** (returns `ok: true`), the tool result is added to the conversation
- If the hook **rejects** (returns `ok: false`), the tool result is blocked and replaced with the rejection reason
- Hooks can provide `additionalContext` that gets injected as a user message

## Hook Execution Types

### Prompt Hooks

Prompt hooks use an LLM to evaluate the hook context. You provide a prompt that instructs the LLM how to evaluate the situation.

```yaml
hooks:
  Stop:
    - type: prompt
      prompt: |
        Analyze if the agent should stop. The agent's final response is:

        $ARGUMENTS

        Check if the response contains "Task complete." at the end.
        If it does, the agent can stop.
        If it doesn't, the agent should continue working.

        Respond with JSON only:
        - {"ok": true} to allow stopping
        - {"ok": false, "reason": "Please complete your response with 'Task complete.'"} to continue
      timeout: 30
      model: ReasoningFast  # optional, defaults to ReasoningFast
      maxRejections: 5      # optional, only for Stop hooks
```

**Prompt placeholders:**
- `$ARGUMENTS` - Replaced with the full hook context as JSON. If not present, context is appended to the end.

**Model options:**
- `ReasoningHeavy` - Complex, multi-step reasoning
- `ReasoningFast` - Good reasoning with low latency (default)
- `GeneralPurpose` - Mixed tasks with balanced accuracy
- `SmallFast` - Lowest cost/latency
- `LongContext` - Handling long documents
- `Eval` - Grading and assessment
- Or use a specific deployment name (e.g., `gpt-4.1`)

### Command Hooks

Command hooks execute a shell script to evaluate the hook context. The script receives the context as JSON via stdin and outputs JSON to stdout.

```yaml
hooks:
  PostToolUse:
    - type: command
      matcher: "*"  # required for PostToolUse - regex pattern to match tool names
      timeout: 30
      failMode: allow  # 'allow' or 'block' - what to do if the script fails
      script: |
        #!/usr/bin/env python3
        import sys
        import json

        # Read hook context from stdin
        context = json.load(sys.stdin)
        tool_name = context.get('tool_name', 'unknown')

        # Your validation logic here
        if some_condition:
            output = {"decision": "allow"}
        else:
            output = {"decision": "block", "reason": "Validation failed"}

        print(json.dumps(output))
        sys.exit(0)
```

**Script execution:**
- Scripts run in a sandboxed code interpreter environment
- Bash scripts (`.sh`) and Python scripts (`.py`) are supported
- Use shebang (`#!/bin/bash` or `#!/usr/bin/env python3`) to specify interpreter
- Scripts have access to common tools like `jq` for JSON parsing

**Exit codes:**
- `0` with output - Parse stdout as JSON for the decision
- `0` with no output - Allow (no objection from hook)
- `2` - Always blocks the action, stderr becomes the reason (ignores `failMode`)
- Other - Uses `failMode` to determine behavior

## Input Schema (Hook Context)

The hook receives context as JSON via stdin (command hooks) or as the `$ARGUMENTS` placeholder (prompt hooks).

### Common Fields (All Hook Types)

```json
{
  "hook_event_name": "Stop",           // "Stop" or "PostToolUse"
  "agent_name": "my_agent",            // Name of the current agent
  "current_turn": 5,                   // Current turn number
  "max_turns": 50,                     // Maximum turns allowed
  "execution_summary": "/path/to/transcript.txt"  // File path to chat transcript (JSON format)
}
```

### Stop Hook Additional Fields

```json
{
  "final_output": "Here is my response...",  // The response the agent is about to return
  "stop_hook_active": false,                  // True if a stop hook has already rejected
  "stop_rejection_count": 0                   // Number of times stop hooks have rejected
}
```

### PostToolUse Hook Additional Fields

```json
{
  "tool_name": "ExecutePythonCode",    // Name of the executed tool
  "tool_input": { ... },               // Arguments passed to the tool
  "tool_result": "...",                // Output from the tool
  "tool_succeeded": true               // Whether execution succeeded
}
```

### Transcript File Format

The `execution_summary` field contains a path to a JSON file with the chat transcript:

```json
{
  "items": [
    {"type": "text", "role": "user", "text": "What is 2+2?"},
    {"type": "text", "role": "assistant", "text": "Let me calculate that."},
    {"type": "function_call", "role": "assistant", "function_name": "ExecutePythonCode", "parameters": "{\"code\": \"print(2+2)\"}"},
    {"type": "function_result", "call_id": "call_123", "result": "[Result filtered for brevity]"},
    {"type": "text", "role": "assistant", "text": "The answer is 4. Task complete."}
  ]
}
```

**Item types:**
- `text` - Text message with `role` and `text` fields
- `function_call` - Tool invocation with `role`, `function_name`, and `parameters`
- `function_result` - Tool result with `call_id` and `result` (filtered for brevity)
- `critic_feedback` - Critic feedback with `feedback` field

## Output Schema (Hook Result)

Hooks must output JSON to stdout (command hooks) or respond with JSON (prompt hooks). Command hooks may also use exit codes to control behavior (see below).

### Response Formats

There are two supported formats. Use whichever is more convenient for your hook type:

**Simple `ok` format** (recommended for prompt hooks):
```json
{"ok": true}
{"ok": false, "reason": "Please include more details."}
```

**Expanded `decision` format** (recommended for command hooks):
```json
{"decision": "allow", "hookSpecificOutput": {"additionalContext": "Do these steps next..."}}
{"decision": "block", "reason": "Please include more details."}
```

The expanded `decision` format is preferred for command hooks because it's more explicit and easier to construct in shell scripts. The simpler `ok` format works well for prompt hooks where an LLM generates the response.

**Important:** When rejecting/blocking, you **must** provide a `reason`. A rejection without a reason is treated as approval.

### Simple Exit Code Responses (Command Hooks Only)

For simple command hooks, you can use exit codes instead of outputting JSON:

| Exit Code | Behavior |
|-----------|----------|
| `0` with no stdout | **Allow** - treated as approval |
| `0` with JSON stdout | Parse the JSON for decision |
| `2` | **Block** - always rejects (stderr becomes the reason) |
| Other | Use `failMode` setting (`allow` or `block`) |

**Example - Simple validation using only exit codes:**
```bash
#!/bin/bash
CONTEXT=$(cat)
FINAL_OUTPUT=$(echo "$CONTEXT" | jq -r '.final_output // empty')

if [[ "$FINAL_OUTPUT" == *"Task complete."* ]]; then
  exit 0  # Allow (no output needed)
else
  echo "Missing 'Task complete.' marker" >&2
  exit 2  # Block (stderr is used as reason)
fi
```

### What Gets Injected Into the Conversation

Understanding what the agent sees is important for writing effective hooks:

**Stop Hooks:**
| Hook Result | What Happens |
|-------------|--------------|
| Allow (`ok: true`) | Agent stops, returns its response to the user |
| Block (`ok: false` + `reason`) | The `reason` is injected as a **user message**, agent continues working |

**PostToolUse Hooks:**
| Hook Result | What Happens |
|-------------|--------------|
| Allow (`ok: true`) | Tool result is added to conversation normally |
| Allow + `additionalContext` | Tool result added, then `additionalContext` injected as **user message** |
| Block (`ok: false` + `reason`) | Tool result replaced with `[Hook Blocked Tool Result] {reason}` |
| Block + `additionalContext` | Tool result replaced with block message, then `additionalContext` injected as **user message** |

**Key insight:** The `additionalContext` field is injected as a user message regardless of whether you allow or block. This makes it useful for:
- Audit logging that the agent can see
- Providing guidance or hints to the agent
- Adding context that should influence the agent's next action

### With Additional Context

```json
{
  "decision": "allow",
  "hookSpecificOutput": {
    "additionalContext": "[AUDIT] Tool 'Edit' was used to modify config.yaml"
  }
}
```

This works for both allow and block responses. The `additionalContext` is always injected as a user message after the hook decision is applied.

## PostToolUse Matcher

PostToolUse hooks require a `matcher` field that specifies which tools the hook applies to:

```yaml
hooks:
  PostToolUse:
    # Match all tools
    - type: command
      matcher: "*"
      script: ...

    # Match specific tools (regex)
    - type: command
      matcher: "Edit|Write"
      script: ...

    # Match tools by pattern
    - type: command
      matcher: "Execute.*"
      script: ...
```

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `type` | string | `prompt` | Hook execution type: `prompt` or `command` |
| `timeout` | int | `30` | Timeout in seconds |
| `failMode` | string | `allow` | For command hooks: `allow` or `block` on errors |
| `maxRejections` | int | `3` | For prompt Stop hooks: max rejections before forcing stop |
| `model` | string | `ReasoningFast` | For prompt hooks: model scenario or deployment name |
| `matcher` | string | - | For PostToolUse hooks: regex pattern for tool names |

## Complete Examples

### Stop Hook - Require Completion Marker (Prompt)

```yaml
hooks:
  Stop:
    - type: prompt
      prompt: |
        Check if the agent's response ends with "Task complete."

        Response to check:
        $ARGUMENTS

        Respond with:
        - {"ok": true} if the response contains "Task complete."
        - {"ok": false, "reason": "Please end your response with 'Task complete.'"} otherwise
      timeout: 30
      maxRejections: 5
```

### Stop Hook - Require Completion Marker (Command)

```yaml
hooks:
  Stop:
    - type: command
      timeout: 30
      failMode: allow
      script: |
        #!/bin/bash
        CONTEXT=$(cat)

        # Extract transcript path and read the last assistant message
        TRANSCRIPT_PATH=$(echo "$CONTEXT" | jq -r '.execution_summary // empty')

        if [[ -f "$TRANSCRIPT_PATH" ]]; then
          LAST_MSG=$(cat "$TRANSCRIPT_PATH" | jq -r '
            [.items[] | select(.type == "text" and .role == "assistant")] | last | .text // empty
          ')
        fi

        if [[ "$LAST_MSG" == *"Task complete."* ]]; then
          echo '{"decision": "allow"}'
        else
          echo '{"decision": "block", "reason": "Please end your response with 'Task complete.' when you have fully finished your task"}'
        fi
```

### Stop Hook - Simple Exit Code Version

For simple validations, you can use exit codes without JSON output:

```yaml
hooks:
  Stop:
    - type: command
      timeout: 30
      failMode: allow
      script: |
        #!/bin/bash
        CONTEXT=$(cat)
        FINAL_OUTPUT=$(echo "$CONTEXT" | jq -r '.final_output // empty')

        if [[ "$FINAL_OUTPUT" == *"Task complete."* ]]; then
          exit 0  # Allow - no output needed
        else
          echo "Please end your response with 'Task complete.'" >&2
          exit 2  # Block - stderr becomes the reason
        fi
```

### PostToolUse Hook - Audit All Tool Usage

```yaml
hooks:
  PostToolUse:
    - type: command
      matcher: "*"
      timeout: 30
      failMode: allow
      script: |
        #!/usr/bin/env python3
        import sys
        import json

        context = json.load(sys.stdin)
        tool_name = context.get('tool_name', 'unknown')

        # Log to stderr (visible in server logs)
        print(f"Tool used: {tool_name}", file=sys.stderr)

        # Allow with audit message injected into conversation
        output = {
            "decision": "allow",
            "hookSpecificOutput": {
                "additionalContext": f"[AUDIT] Tool '{tool_name}' was executed."
            }
        }
        print(json.dumps(output))
```

### PostToolUse Hook - Block Dangerous Commands

```yaml
hooks:
  PostToolUse:
    - type: command
      matcher: "Bash|ExecuteShellCommand"
      timeout: 30
      failMode: block
      script: |
        #!/usr/bin/env python3
        import sys
        import json
        import re

        context = json.load(sys.stdin)
        tool_input = context.get('tool_input', {})
        command = tool_input.get('command', '') if isinstance(tool_input, dict) else ''

        # Block dangerous commands
        dangerous_patterns = [r'\brm\s+-rf\b', r'\bsudo\b', r'\bchmod\s+777\b']

        for pattern in dangerous_patterns:
            if re.search(pattern, command):
                print(json.dumps({
                    "decision": "block",
                    "reason": f"Dangerous command pattern detected: {pattern}"
                }))
                sys.exit(0)

        print(json.dumps({"decision": "allow"}))
```

## Best Practices

1. **Always provide a reason when rejecting** - Rejections without reasons are treated as approvals
2. **Use appropriate timeouts** - Long-running hooks can slow down agent execution
3. **Handle errors gracefully** - Use `failMode: allow` unless you need strict enforcement
4. **Be specific with matchers** - Overly broad PostToolUse matchers can cause performance issues
5. **Test hooks thoroughly** - Hooks that always reject can cause infinite loops (mitigated by `maxRejections`)
6. **Log to stderr** - Use stderr for debugging output; stdout is parsed as the hook result
