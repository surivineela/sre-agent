---
name: icm_incident_management
description: |
  Load this skill when the user asks to work with Azure ICM incidents: acknowledge, mitigate, resolve, reopen, transfer, adjust severity, search/list incidents, retrieve or summarize discussions, custom fields, repair items, attachments, or link/unlink/correlate incidents.
  Do NOT load for general Azure resource discovery or diagnostics (covered by the main system prompt). Use only for incident lifecycle, context enrichment, and relationship/correlation management.
tools:
  - AcknowledgeIncident
  - MitigateIncident
  - ResolveIncident
  - TransferIncident
  - GetIncidentInfo
  - GetCustomFields
  - GetDiscussionEntries
  - GetAlertingDiscussionEntry
  - SearchIncidents
  - GetCurrentUtcDateTime
  - PostDiscussionEntry
  - DowngradeSeverity
  - UpdateIncidentSeverity
  - AddTagToIncident
  - AddKeywordToIncident
  - GetIcmCorrelationAndLinkingRules
  - GetLinkedRelatedIncidentInfo
  - AddRelatedIncidentLink
  - RemoveRelatedIncidentLink
  - GetParentIncidentInfo
  - AddParentIncidentLink
  - RemoveParentIncidentLink
  - GetChildIncidentsInfo
  - GetIncidentRepairItems
  - AddIncidentAttachmentFromFile
  - AddIncidentAttachmentFromContent
  - ListIncidentAttachments
---

# Azure ICM Incident Management Skill

## Purpose & Scope

Manage Azure ICM incidents (ownership, mitigation, resolution, severity changes, transfers, correlation/linking, searches, discussions, repair items, attachments). This skill augments the system prompt; it does not redefine global safety / conciseness rules. Use only for incident lifecycle and context—not general Azure resource discovery.

## Core Principles (inherits global hierarchy)

- Be concise: give only requested incident data unless the task requires more (e.g., resolution summary).
- Timestamp discussion updates in UTC (use GetCurrentUtcDateTime if needed).
- Separate facts vs. hypotheses; mark assumptions clearly.
- Never fabricate incident fields—retrieve before summarizing.

## Lifecycle Actions (what to capture in discussion notes)

| Action | Minimum Contents | Optional Enhancements |
|--------|------------------|-----------------------|
| Acknowledge | Time, ownership, current impact summary, immediate next step | Severity review + justification |
| Mitigate | Action taken, start/end times, expected vs. observed result | Residual impact + follow-up plan |
| Resolve | Verification evidence, root cause, contributing factors | Prevention items w/ owner & due date |
| Reopen | Reason + new signal/metric, prior resolution reference | Scope change details |
| Transfer | Target team/queue, reason, pending actions | Key artifacts (IDs, attachment refs) |
| Severity Change | New severity, justification (impact change) | Trigger metrics (rates, latency, region) |

When adding a note: Impact → Action → Evidence → Result (one line each if possible).

## Information Retrieval Guidelines

- GetIncidentInfo first for basic fields (title, severity, status, owners, timestamps, service/component, regions).
- Use GetCustomFields for enriched routing or impact data (VIP, deployment ring, build/commit ID, correlation IDs, tenant/subscription).
- Use GetDiscussionEntries to summarize: major decisions, mitigation steps, escalations, ownership changes, verification outcomes.
- Use GetIncidentRepairItems to list change tickets, hotfixes, rollbacks, feature flag operations; map items to timeline impacts.
- Relationship tools (parent/child/related) only after confirming need (e.g., overlapping scope, shared correlation ID, same deployment wave).

## Correlation & Linking

- Parent/Child: Clear hierarchical scope (global vs. regional/component impacts).
- Related: Shared symptoms/signals without hierarchy.
- Avoid linking on vague similarity (e.g., same service alone). Require ≥2 strong signals (e.g., same build + correlation ID).

For each link/unlink: add a discussion entry with (Type, Target ID(s), Evidence signals, Expected triage benefit).

## Attachments

Attach only artifacts that add diagnostic or audit value (logs, metrics snapshots, timelines, runbooks, config diffs, network traces).
Best practices: descriptive filename, brief caption, redact sensitive data, reference in a discussion with key finding.

## Search Strategy (SearchIncidents)

1. Start with most discriminating filters (ID if given; else service/component + timeframe).
2. Narrow using severity, region, owner, tags, custom fields (deployment ring, build ID, correlation ID).
3. Provide summary list (ID, title, severity, status) unless user requests full detail.
4. Suggest historical similar incidents only if user asks for patterns or mitigation ideas.

## Discussion Quality Checklist

- UTC timestamp present
- Impact clearly stated (who/what, measurable symptom)
- Action described (specific change, flag, rollback)
- Evidence (metric delta, error rate change)
- Result / next step

## Minimal Examples

- Acknowledge: "09:12 UTC Ownership claimed. Impact: 500s 22% in West Europe API v2. Next: rollback build 2025.10.24.1."
- Mitigation: "09:27 UTC Disabled flag FF-Checkout-Async. Error rate 25%→3% (5m). Residual: sporadic timeouts; investigating DB pool."
- Resolve: "10:18 UTC Impact cleared. Root cause: memory leak in 2025.10.24.1. Rolled back + hotfix 2025.10.24.1a. 95p latency baseline. Prevention: add leak detection (owner SRE-Perf, due 2025-11-07)."
- Link rationale: "Linked INC-4321 child of INC-4300 (same ring R3 + correlation c-7f12 + identical error signature)."

## Summary Pattern

Direct answer (1–2 sentences) → requested incident data → next step only if user intent requires action.

## Avoid

- Overly verbose historical dumps (summarize instead).
- Linking on weak similarity.
- Attaching large raw logs without a summarized finding.

## Quick Reference (Tool Selection)

| Need | Tool |
|------|------|
| Basic incident fields | GetIncidentInfo |
| Custom routing/impact data | GetCustomFields |
| Timeline & decisions | GetDiscussionEntries |
| Active alert entry | GetAlertingDiscussionEntry |
| Repair/work items | GetIncidentRepairItems |
| Search set | SearchIncidents |
| Parent/Child/Related info | GetParentIncidentInfo / GetChildIncidentsInfo / GetLinkedRelatedIncidentInfo |
| Add relationship | AddParentIncidentLink / AddRelatedIncidentLink |
| Remove relationship | RemoveParentIncidentLink / RemoveRelatedIncidentLink |
| Attach artifact | AddIncidentAttachmentFromFile / AddIncidentAttachmentFromContent |
| List attachments | ListIncidentAttachments |
| Add discussion | PostDiscussionEntry |
| Severity change | DowngradeSeverity / UpdateIncidentSeverity |

Always align output with global conciseness and safety constraints.
