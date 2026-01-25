---
name: cannot_connect_to_vm
description: The "Cannot Connect to VM" skill specializes in diagnosing and addressing connectivity or boot issues (RDP/SSH) with Azure Virtual Machines, leveraging targeted tools and resources for troubleshooting while adhering to strict validation steps and prerequisites.
tools:
  - DiagnoseVmConnectivityIssues
  - GetArmResourceAsJson
  - GetVirtualMachineBootStateAsJson
  - AnalyzeVmScreenshot
  - AnalyzeVmSerialLog
  - RunAzCliReadCommands
  - PowerOnVirtualMachine
---

# Cannot Connect to Azure VM Skill

## Overview
Guides safe, concise triage when users cannot reach an Azure VM via RDP or SSH, or suspect boot issues. Focus on: confirming resource identity, validating prerequisites (power state, boot diagnostics), mapping known error strings, then invoking targeted diagnostics. Align with global hierarchy: Safety → Accuracy → Conciseness → Efficiency.

## Core Principles

1. Never run diagnostics before confirming full VM resourceId (starts with `/subscriptions/`).
2. Collect power state, OS type, boot diagnostics flag once; cache results—avoid redundant fetches.
3. Prompt exactly once for the RDP / SSH error message; no repeated asks.
4. Map error to known guidance strictly (substring case‑insensitive, first match wins); never fabricate a mapping.
5. Stop early if VM not running (offer power on) or boot diagnostics disabled (offer enable) before deeper steps.
6. Use `DiagnoseVmConnectivityIssues` only after prerequisites satisfied; only call screenshot / serial log tools if diagnosis explicitly instructs.
7. Escalate / pause after two identical failures of the same action.
8. Power operations (start) require explicit user confirmation (impact minimal but still a state change).
9. Do not perform advanced OS recovery (disk detach, extension resets) – out of scope.

## Internal Tools Reference (not surfaced verbatim to user)

| Purpose | Tool |
|---------|------|
| VM boot state (power) | GetVirtualMachineBootStateAsJson |
| ARM properties (osType, diagnostics) | GetArmResourceAsJson |
| Connectivity diagnosis | DiagnoseVmConnectivityIssues |
| Screenshot analysis | AnalyzeVmScreenshot |
| Serial log analysis | AnalyzeVmSerialLog |
| Power on VM | PowerOnVirtualMachine |
| Generic CLI (other ops) | RunAzCliReadCommands (writes via azure CLI skill) |

## Progressive Discovery & When to Open Other Skills

Open an additional skill only when a trigger below is met; load one, act, then return here if a new domain emerges.

| Trigger | Action | Rationale |
|---------|--------|-----------|
| VM resourceId missing or ambiguous after one clarification | Use SearchResource with filters: SearchResource(resourceTypes: ["microsoft.resources/subscriptions"]) for subscriptions, SearchResource(resourceTypes: ["microsoft.resources/subscriptions/resourcegroups"], subscriptionId: "a1b2c3d4-e5f6-7890-abcd-ef1234567890") for resource groups, SearchResource(resourceName: "vm-name", resourceTypes: ["microsoft.compute/virtualmachines"]) for VMs | Obtain precise VM ID before diagnosis |
| Need to enable boot diagnostics or run supporting CLI (e.g., set diagnostics storage) | Open `azure_cli_command_executor` | Safe enablement / parameterized CLI execution |
| Need performance or availability trend beyond current snapshot (e.g., intermittent connectivity correlating with CPU spikes) | Open `metrics_and_chart_visualization` | Correlate resource metrics with connection failures |

## Input Gathering

1. Confirm VM resourceId. If absent → use SearchResource: SearchResource(resourceTypes: ["microsoft.resources/subscriptions"]) to find subscriptions, SearchResource(resourceName: "vm-name", resourceTypes: ["microsoft.compute/virtualmachines"]) to locate the VM (returns resourceId in results), then proceed.
2. Prompt once: “Do you have the exact RDP / SSH error message? Paste it so I can map known guidance. If not, say ‘no’.”
3. Fetch prerequisites:
   - `GetVirtualMachineBootStateAsJson` → powerState
   - `GetArmResourceAsJson` → osType, bootDiagnosticsEnabled
   Cache values; reuse.

## Error Mapping Table

Perform case‑insensitive substring match (first match only). If none matched or user said “no” → leave tsgFileName empty.

| Substring Pattern | tsgFileName |
|-------------------|-------------|
| A licensing error occurred while the client was attempting to connect | vmhealthsignal_57876450-c7df-4f23-8aea-a34797fe7e2d |
| A user account restriction is preventing you from logging on | windows-rdp-account-restriction |
| Access is denied | windows-rdp-access-denied |
| An authentication error has occurred. The function requested is not supported | windows-credssp |
| An authentication error has occurred. The Local Security Authority cannot be contacted | windows-rdp-authentication-errors |
| Because of a Protocol error detected at the client (code 0x1104) | VMCannotRDP-protocol_error_0x1104 |
| Configuring remote session | windows-rdp-configuring-remote-session |
| requires Network Level Authentication | vmhealthsignal_451e7e7e-0aae-4145-ae1b-e761548a36d7 |
| disconnected because there are no Remote Desktop License Servers | vmhealthsignal_57876450-c7df-4f23-8aea-a34797fe7e2d |
| restricted the times during which you may log in | windows-restrict-login |
| restricted the types of logon | VMCannotRDP-administrator_restricted_the_types_of_logon |
| trust relationship between this workstation and the primary domain failed | windows-rdp-broken-secure-channel |
| This computer can't connect to the remote computer | windows-rdp-troubleshoot-eventid |
| Too many admins session open | windows-admin-sessions |
| User profile cannot be loaded | VMCannotRDP-user_profile_cannot_be_loaded |
| We can't sign into your account | windows-rdp-we-cant-sign-into-your-account |
| Your Remote Desktop Services session has ended | windows-rdp-session-ended-error |
| You must change your password before logging on the first time | windows-change-password |

## Prerequisite Decisions

| Condition | Action |
|-----------|--------|
| powerState not running | Present state; ask approval to call `PowerOnVirtualMachine`; stop further diagnostics until running |
| bootDiagnosticsEnabled false | Offer CLI enablement via azure CLI skill; stop until enabled |
| prerequisites satisfied | Proceed to diagnosis |

## Diagnosis Execution

Invoke `DiagnoseVmConnectivityIssues` with: resourceId, osType, tsgFileName (may be empty).
Do not directly call screenshot / serial log tools unless instructed by returned guidance. Avoid duplicate calls.

## Post-Diagnosis Handling

| Result Type | Response |
|-------------|----------|
| Direct resolution (mapped tsg) | Present concise fix; offer CLI execution if commands needed (via azure CLI skill) |
| Indicates need for screenshot / serial log | Call only the specifically instructed tool; summarize findings |
| No clear cause | State outcome; suggest broader investigation (metrics skill if performance suspected) |
| Requires config change (e.g., enable NLA) | Offer precise CLI or portal guidance; get approval before changes |

## Constraints

1. Never fabricate `tsgFileName`.
2. Never alter CLI command output from tools.
3. No repeated prompting for error text.
4. No OS‑level recovery tasks.
5. Respect early stop conditions.

## Lightweight Self‑Check (internal)

Before proceeding each phase ensure: resourceId confirmed → prerequisites cached → mapping performed (or explicitly none) → early stops honored → single diagnostic invocation.

## Example (Abbreviated)

User cannot RDP; error: “The Local Security Authority cannot be contacted”.
1. Confirm resourceId.
2. Prompt for error (received string) → map to `windows-rdp-authentication-errors`.
3. Fetch powerState + boot diagnostics.
4. If running and diagnostics enabled → call diagnosis with mapped tsg.
5. Present guidance from result; offer CLI steps if configuration adjustment needed.

## Cross References

- `azure_cli_command_executor` – enable boot diagnostics, execute minor configuration commands (with approval).
- `metrics_and_chart_visualization` – correlate intermittent connectivity with performance trends when diagnosis inconclusive.

## Out of Scope
Password resets, disk detach/reattach, manual registry edits, domain trust repair—refer to appropriate specialized processes if requested.

## Completion Format
"[OK] Connectivity diagnosis completed – {key_finding_or_early_stop}. Next: {next_action_or_closure}."
