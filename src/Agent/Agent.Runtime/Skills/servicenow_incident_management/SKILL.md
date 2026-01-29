---
name: servicenow_incident_management
description: |
  Load this skill when the user requests to:
  - Acknowledge or take ownership of a ServiceNow incident (INC*).
  - Add internal (work notes) or external (comments) updates.
  - Decide between mitigation vs full resolution, or clarify correct state transition.
  - Provide or improve resolution / close notes, or choose a resolution code.
  - Structure high‑quality audit entries (who/what/when/why/next).
  Do NOT load for purely Azure resource discovery or diagnostics that do not involve ServiceNow incident state or communication.
tools:
  - PostServiceNowDiscussionEntry
  - AcknowledgeServiceNowIncident
  - ResolveServiceNowIncident
---

# ServiceNow Incident Management

## Purpose

Concise, unambiguous guidance for the ServiceNow incident lifecycle (acknowledge → investigate → communicate → mitigate → resolve) with clear separation of internal (work notes) vs external (comments) updates and auditable state changes.

## Scope & Assumptions

* Applies only to ServiceNow incidents (IDs like INC00*).
* Complementary to global hierarchy: Safety > Accuracy > Conciseness > Efficiency (do not override it).
* No resource discovery instructions here (retired); focus purely on incident state & communication quality.
* Never include secrets or raw credentials. Redact sensitive diagnostic output.

## Core Capabilities

* Retrieve and validate an incident before any action.
* Establish ownership (acknowledge) and set correct state/assignment.
* Add internal (work notes) or external (comments) discussion entries.
* Distinguish mitigation (impact reduced) from resolution (issue fixed + validated).
* Produce complete, concise close notes (cause, fix, validation, prevention).
* Maintain audit trail: who / what / when / why / next.
* Handle edge cases: missing incident, permission issues, conflicting state, concurrent updates.

## Lifecycle Flow (follow in order)

1. Confirm inputs: incident ID (INC*), intended action, note type if adding entry.
2. Retrieve & validate: state, priority, assignment group, caller, category/subcategory, CI, Azure context, existing notes/comments.
3. Plan update: decide fields/state transition; choose note type (internal vs external); gather Azure evidence (alerts, metrics, IDs).
4. Execute atomically: perform one logical change per operation; write structured note.
5. Verify & report: re-read incident; confirm fields changed; summarize outcome + next step (if any).

## Actions

### Acknowledge

Use when first assuming ownership.
Preconditions: State is New/Open; you have/assign proper group.
Updates: State → In Progress (or equivalent); assignment group/user set; add work note.
Work note template:
"Acknowledged at [UTC timestamp] by [role/name]. Initial triage started. Context: [Azure resource/ref]. Next update by [UTC]."

### Add Discussion Entry

Choose note type:

| Use | Work Notes (internal) | Comments (external) |
|-----|-----------------------|---------------------|
| Audience | Support / ops teams | Caller / requester |
| Content | Technical detail, diagnostics, escalation steps | Plain-language status & impact |
| Include | Commands run, metrics, correlation IDs | Summary, improvement, ETA |

Guidelines (both): start with status/change; include UTC timestamp; reference Azure signals (Service Health, alerts, KQL queries); give next checkpoint if ongoing.

### Mitigate

Definition: Impact reduced, not fully resolved; symptoms improved.
Updates: State stays In Progress / moves to On Hold if waiting external fix; work notes with mitigation steps + evidence (before/after metrics). Optional external comment summarizing improvement + next ETA.

### Resolve

Preconditions: Symptom cleared; fix applied & validated; cause known or documented.
Required updates:
* State → Resolved (or Closed per workflow)
* Resolution Code / Cause set appropriately
* Close Notes written (see template)
* Work notes: diagnostics performed, validation evidence
* External comment: plain summary + any user action

Close Notes template:
"Root cause: [cause]. Fix: [action] at [UTC timestamp]. Validation: [evidence]. Preventive: [follow-up/automation]."

## Note Templates (use/adapt as needed)

* Acknowledge: "Acknowledged at 2025-11-05T14:23Z by Incident Mgmt. Triage started. Context: [resource]. Next update by 15:00Z."
* Work note (progress): "2025-11-05T14:40Z: Ran network diagnostics; packet loss improved 40%→5%. Investigating LB health probes. Next: verify backend pool."
* External comment: "Connectivity improving in East US; mitigation in progress. Next update by 15:30Z UTC."
* Resolution comment (external): "Issue resolved. Misconfigured NSG rule corrected; traffic normal. No user action required."

## Field & Validation Checklist

Before update & after update confirm:
* State, Priority, Assignment Group, Assigned To
* Short Description, Description
* Category/Subcategory, CI, Azure resource linkage
* Work Notes vs Comments separation
* Resolution Code + Close Notes (on resolve)
* Timestamps (UTC) present in notes

## Best Practices

* Always read incident immediately before writing; re-read after to confirm.
* Keep internal notes technical; keep external comments clear & jargon-free.
* Reference Azure context: subscription, resource group, resource name/type, region, alert IDs.
* One logical change per operation (avoid bundling unrelated updates).
* Include who performed action (role/name) for audit.
* If concurrent update detected: re-fetch, reconcile, then proceed.
* Escalate rather than guess when data missing or permissions insufficient.

## Error & Edge Cases

* Not found: stop; suggest verifying ID.
* Permission denied: report; request access or alternative contact.
* Already resolved: confirm if reopening needed before modifying.
* Locked/conflict: re-fetch latest; merge context; retry.
* Ambiguous action request: ask for clarification (state what’s unclear specifically).

## Examples

1. Acknowledge: INC0023456 (Networking) → State: In Progress; work note with triage start + alert ID.
2. Internal progress note: packet loss reduction with metrics + next diagnostic step.
3. External status: improvement + next ETA.
4. Resolve: NSG rule fix; validation metrics normal; close notes + external summary.

## Audit & Security

* Every note: UTC timestamp + actor (role/name) + concise action/result.
* No secrets (keys, tokens, PII). Redact sensitive values; summarize instead.
* Provide correlation IDs / alert IDs when they add traceability.

## Completion Signal

After successful action: brief summary (state changed / note added) + next step or "No further action required" if resolved.
