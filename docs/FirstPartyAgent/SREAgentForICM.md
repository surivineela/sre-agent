# SRE Agent for Incident Management

## 1. Agent Configuration & Teams Integration

### Prepare Configuration (JSON)

First create the Agent Configuration (JSON) file. Read more about the [file specs](/docs/FirstPartyAgent/AgentConfiguration.md). Send it over to us. We will deploy it to the agent and surface the agent through Teams for manual validation of the agent.

### Teams Integration

* The agent is integrated into a dedicated Teams chat for interactive communication and operational visibility.
* **Verbosity Control**: When the `SendLogsToTeams` environment variable is set to `true` on the production endpoint, the agent sends detailed execution logs to the chat. This setting can be toggled as needed.

### State Management

* To clear the agent’s context or reset its memory, send the message: `clear state` in the Teams chat.

---

## 2. Graduation Process & Operational Stages

The agent is introduced into production in stages to ensure safe and effective adoption:

| Stage | Mode          | Capabilities                                                                  | Description                                                                          |
| ----- | ------------- | ----------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| 1     | Read-Only     | Executes read actions; shows intended writes but doesn't execute them         | Used to verify parsing, decision logic, and read functionality without side effects. |
| 2     | Approval Mode | Executes both read and write actions, but seeks human confirmation for writes | Adds control and validation by requiring manual approval for state-altering actions. |
| 3     | Auto Mode     | Fully autonomous in reading and writing                                       | Enables hands-free incident handling

> **Note:** Teams chat modes can be skipped or accelerated depending on confidence in the agent's reliability.

---

## 3. Interaction Modes & Commands

### Read-Only Mode (Stage 1)

* **Purpose**: Validate parsing and read execution.
* **Behavior**: Executes read operations and shows what write actions would be taken.

### Approval Mode (Stage 2)

* **Purpose**: Enable controlled write actions with human confirmation.
* **Behavior**: Processes incidents automatically and prompts before executing write actions.

**Command Example:**

```
Process this incident in APPROVAL_MODE: 123432434
```

### Auto Mode (Stage 3)

* **Purpose**: Enable hands-free, fully automated operation.
* **Behavior**: Automatically processes incidents, executing both read and write actions.

**Command Example:**

```
Process this incident in AUTO_MODE: 123432434
```

---

## 4. Usage Tips & Best Practices

* **Reset Agent State**: Use `clear state` in Teams to clear memory or recover from unexpected behavior.
* **Control Verbosity**: Use the `SendLogsToTeams` environment variable to manage log output.
* **Adapt Progression**: Skipping stages or advancing quickly is acceptable if the agent proves trustworthy during earlier tests.

---

## 5. Next Steps

1. Finalize and submit the configuration JSON file.
2. Deploy the configuration to the production endpoint.
3. Begin testing in Teams using Read-Only mode.
4. Gradually progress through Approval Mode to Auto Mode as confidence builds.

**Contact**: For support or questions, reach out to the SRE tooling team via Teams or email.