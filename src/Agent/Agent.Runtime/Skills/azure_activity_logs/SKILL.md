---
name: azure_activity_logs
description: Use this skill when you need to understand "what changed" in Azure, based on Activity Logs, for one or more resources, resource groups, or subscriptions. It focuses on WRITE and DEPLOYMENT events, builds a clear timeline of important changes, highlights high-risk or failed operations, and explains who made the change and when. The skill is especially useful when an incident may be caused by a recent configuration or platform change, or when deployments are failing or behaving unexpectedly. It produces concise summaries, timelines, risk assessments, and suggested rollback or mitigation options (including draft az CLI commands and issue/work item drafts) while keeping noise low and calling out uncertainty when data is incomplete.
tools:
  - GetActivityLogsSummary
  - AnalyzeDeploymentFailures
  - GetChangeHistory
  - ShowChangeDiffViewer
---

# Azure Activity Logs Analysis Skill


## Purpose

Use Azure Activity Logs to answer "what changed, when, and by whom" for Azure resources and deployments, and to propose safe mitigation options when changes are likely related to an incident.

This skill should be used when:

- The incident is suspected to be caused by a configuration or platform change.
- You need to understand recent deployments or WRITE operations for one or more resources, subscriptions, or resource groups.
- You are investigating failed or partially failed deployments.

## Inputs & Time Window

- Typical inputs: resourceId(s), subscription or broader scope(s), time window, optional correlationId.
- Default time window: last 24 hours.
- If there are few relevant events, you may extend the window up to 72 hours and clearly mark older events as context.
- Prefer the user’s local time zone if provided; otherwise use UTC and clearly label timestamps as UTC. Preserve raw UTC values in any evidence.

## Event Selection

Focus primarily on WRITE (PUT/POST/PATCH/DELETE) and DEPLOYMENT operations.

Include GET/read operations only when:

- The user explicitly asks about access or read anomalies, or
- GET patterns appear suspicious (for example, repeated probing or unauthorized attempts).

## Grouping & Correlation

- Prefer correlationId as the primary grouping key.
- When correlationId is missing, approximate groups using:
  - Time proximity (for example, within ±2 minutes).
  - Same actor (principal/app) and/or same resource.
  - Deployment name or event source where applicable.
- If you cannot confidently correlate events, state that explicitly and describe what you attempted.

## Actor & Identity

Distinguish between human and automated actors:

- Use principal type, appId / service principal, and managed identity indicators.
- Highlight whether changes were made by a user, service principal, managed identity, or other system component.
- Call out automation vs human-triggered changes when it affects intent, investigation, or rollback options.

## High-Impact Changes to Prioritize

Pay special attention to changes involving:

- Application/runtime: environment variables, secrets, image tag/digest, health probes, feature flags, slots or revisions.
- Network/edge: NSGs, firewalls, Private Endpoints, IP restrictions, TLS settings, hostnames/certificates, Front Door, Application Gateway, or WAF.
- Identity/access: managed identity or service principal assignments, Key Vault policies, RBAC on critical resources.
- Data plane: connection strings, storage firewall/bypass rules, database authentication or parameters.
- Scale/SKU/region: instance counts, autoscale rules, SKU/size changes, region/zone moves, or dependency swaps.

Also prioritize:

- Failed or partially failed operations.
- Deployment failures or repeated retries.

## Risk Assessment

For each important group of changes:

- Assign a risk level: Low, Medium, or High.
- Provide a short rationale for the risk level.
- Describe the blast radius: affected resources, user flows, and downstream dependencies.
- Call out uncertainty explicitly if correlation is weak or data is incomplete.

## Rollback and Mitigation Options

When changes are Medium or High risk, provide options as a short checklist. For example:

1. Targeted revert (minimal change)
   - Identify specific risky properties.
   - Propose reverting only those to last known-good values.
   - Provide draft `az` CLI commands and clearly label destructive vs non-destructive actions.
   - Suggest where to use dry-run or `what-if` style validation when available.

2. Redeploy last known-good
   - Identify the last successful deployment for the affected scope.
   - Propose an `az deployment` command with placeholders for missing details.
   - List any templates or parameters that the operator must provide.

3. Open tracking work item (for IaC revert)
   - Draft the body for a GitHub or Azure Boards item describing:
     - What changed, why it is risky, and the desired revert.
     - Key diffs, actors, timestamps, and correlationId(s).
   - Clearly label this as a draft that may need editing.

When options have trade-offs, recommend one and briefly justify the choice (for example, fastest mitigation vs most durable fix).

## Tool Usage

The following tools are commonly used with this skill:

- `GetActivityLogsSummary`
- `AnalyzeDeploymentFailures`
- `GetChangeHistory`
- `ShowChangeDiffViewer`

Recommended usage pattern:

1. `GetActivityLogsSummary`
   - Use first to retrieve Activity Logs for the specified scope and time window.
   - Filter to WRITE and DEPLOYMENT events and remove obvious noise early.

2. Correlation and grouping
   - When available, pass `correlationId` into other tools such as `ShowChangeDiffViewer`.
   - When `correlationId` is missing, approximate groups using operationId, deployment name or event source, time proximity, and consistent actor/resource.
   - If correlation remains unclear, explain what you attempted and label the group accordingly.

3. `ShowChangeDiffViewer`
   - Use for high-priority groups: failures, high-risk configuration changes, large blast radius, or events close to incident onset.
   - Provide inputs such as `resourceId`, `correlationId` (if any), a relevant anchor time, and `operationName`.

4. `GetChangeHistory`
   - Use to understand historical values and patterns and to identify last known-good values for specific properties.

5. `AnalyzeDeploymentFailures`
   - Use for failed, partial, or suspicious deployments.
   - Summarize error codes, important messages, affected resources, and likely root causes.

Handle tool failures gracefully by reporting what failed, what you attempted next, and the impact of any missing data on your confidence.

## Recommended Investigation Flow

Use a simple, repeatable flow:

1. Use `GetActivityLogsSummary` to gather events for the relevant scope and time window.
2. Group events by `correlationId` when possible and use heuristics when it is absent.
3. Rank groups by importance:
   - Failed operations.
   - High-risk configuration changes.
   - Large potential blast radius.
   - Temporal proximity to the suspected incident.
4. For top-ranked groups:
   - Use `ShowChangeDiffViewer` to understand property-level changes.
   - Use `GetChangeHistory` for baseline values where needed.
5. Use `AnalyzeDeploymentFailures` when deployments are failing or behaving suspiciously.
6. For each important group, assess impact and risk and summarize any uncertainty.
7. Propose rollback and mitigation options, including draft `az` CLI plans or issue/work item drafts when appropriate.
8. Build a concise timeline that connects major changes to observed symptoms.
9. Summarize findings and clearly list recommended next steps for the operator.

## Output Structure

Unless the caller specifies a different format, structure your output as:

1. Summary
   - 3–7 bullets covering key changes, likely cause(s), and top risks.
2. Timeline
   - A table or bullets with: time, actor, resource, operation, correlationId, and a short note.
3. Key Changes
   - A short list of the most important property or configuration changes, referencing any diffs from `ShowChangeDiffViewer`.
4. Impact and Risk
   - For each major group, provide risk level, rationale, blast radius, and uncertainty.
5. Rollback and Mitigation
   - Checklist of options and any generated command templates or draft issues.
6. Next Steps
   - Clear, numbered actions for the operator.

## Drafting Commands and Payloads

- Use placeholder values where data is missing (for example, `<resource-group>`, `<template-file>`).
- Clearly mark destructive actions (such as deletion or scaling down) vs non-destructive actions (such as diff or export).
- Prefer the smallest safe change that addresses the risk; only recommend broad redeployments when needed.
- Suggest `what-if` or other dry-run style checks when supported.

## Handling Uncertainty

When data is incomplete or correlation is weak:

- State what is missing (for example, correlationId or earlier property values).
- Briefly describe what you tried (for example, expanding the time window or adjusting filters).
- Suggest the next data collection step if more information is required (for example, obtaining deployment templates or additional logs).

## Questions for the User

Ask the user for input only when necessary, such as:

- Repository, organization, or project details needed to create an issue or work item.
- Labels or assignees that are important for tracking.
- Preferred rollback strategy when multiple viable options exist.
- Any relevant change freezes or maintenance window constraints.

Keep questions concise, specific, and directly related to the investigation or mitigation.

## Guardrails

- Do not execute deployments or rollbacks; only generate plans and recommendations.
- Be explicit when confidence is low or when correlation is incomplete.
- Keep explanations focused, practical, and oriented toward operator action.

## Quality Check

Before finalizing your response, confirm that:

- You focused on meaningful WRITE and DEPLOYMENT changes.
- You provided a clear chronological view of important events.
- You explained failures and risky changes with enough detail to act on.
- You proposed concrete, safe rollback or mitigation options when warranted.
- The output is concise, structured, and easy for an operator to use.
