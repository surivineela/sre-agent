---
name: tls_minimum_version_upgrade
description: |
  Use this skill when the user requests to plan, perform, monitor, rollback, summarize, or investigate raising the minimum TLS version for one or more applications/services.
  It enables:
  - Preparing a concise, sequential upgrade plan (confirmation required before changes).
  - Executing each upgrade with baseline traffic capture, short observation, anomaly detection, and optional rollback.
  - Providing brief status updates (with UTC timestamps for changes) and a final per-app outcome summary.
  Load immediately after the first explicit TLS upgrade intent or when the user asks for: the upgrade plan, progress status, rollback criteria, anomaly detection approach, or a final summary.
tools:
  - GetSuccessfulRequestVolume
  - SetMinimumTlsVersion
---

# TLS Minimum Version Upgrade Skill

## Purpose

Guide sequential upgrades of the minimum TLS version for one or more applications with minimal disruption: plan (confirmation first), execute changes, monitor for anomalies, rollback if needed, and summarize outcomes.

## Core Objectives

- Produce a concise, ordered upgrade plan (no tool names) and request confirmation.
- For each app: capture baseline traffic, apply new minimum TLS version, observe briefly, detect anomalies, optionally rollback.
- Provide short status updates (include UTC timestamp when a change or rollback occurs).
- Stop early if multiple rollbacks indicate systemic risk.
- Deliver a clear final per-app summary.


## Phases

1. Planning (initial response)
   - Enumerate target apps and desired TLS version.
   - For each app: baseline capture → upgrade → observe → proceed/rollback.
   - Specify: 30s observation window with 10s polling after each change; 30s spacing between apps.
   - Ask for confirmation to proceed. Do not mention tool names.
2. Execution (after confirmation)
   - Use tools explicitly when capturing baseline (`GetSuccessfulRequestVolume`) and changing TLS (`SetMinimumTlsVersion`).
   - Poll traffic every 10s for 30s, compare to baseline.
   - Roll back immediately on a significant post-change drop. Continue unless rollbacks occur for multiple apps; then halt remaining upgrades and report.
3. Completion
   - Summarize outcomes per app: target TLS, baseline, observation metrics, anomaly/rollback status, final TLS.
   - State whether plan finished fully or was halted early.


## Timing & Observation

- Baseline: capture once immediately before change (if volume is zero, anomaly detection is limited; continue normally).
- Observation: 30s window; poll every 10s for successful request volume.
- Proceed to next app after observation window; keep a 30s inter-app gap for monitoring clarity.


## Anomaly Criteria

- Focus on large immediate drops (e.g., near-zero vs. healthy baseline). Minor fluctuations are normal.
- If baseline was zero, treat as “no anomaly detectable”; continue unless other failure signals appear.
- On anomaly: announce detection, rollback to previous TLS, confirm rollback success, move on.
- Multiple anomalies (requiring rollbacks) across distinct apps → halt remaining plan and report partial completion.


## Status Update Content

Include: app name, action (baseline captured / TLS updated / rollback), target TLS, baseline volume, latest observation volume(s) if relevant, anomaly status, UTC timestamp for changes.


## Final Summary Content

For each app list: name, target TLS, baseline volume, representative post-change volumes, anomaly detected (Y/N), rollback (Y/N), final TLS version, notes.


## Tool Usage

- GetSuccessfulRequestVolume: baseline + polling comparisons.
- SetMinimumTlsVersion: apply upgrade and perform rollback.

Do not mention tool names until execution begins.


## Formatting (Markdown Only)

Allowed: headings, lists, bold, italics, underline, strikethrough, blockquotes, fenced code blocks. Avoid tables, HTML, images, checklists.


## Example (Initial Plan – no tool names)

1. Validate app list and target TLS version.
2. For each app sequentially: capture baseline; upgrade TLS; observe 30s (poll every 10s) for anomalies; rollback if needed.
3. 30s gap between apps.
4. Provide final per-app summary.
5. Request confirmation to execute.


## Example Status Update (Execution)

- Updated TLS for "checkout-api" to 1.2 at 2025-01-30T01:45:13Z. Baseline: 1,245/min. Beginning 30s observation (10s polling).


## Example Anomaly & Rollback

- Anomaly detected for "billing-service" at 2025-01-30T01:45:23Z (baseline 820/min → 0/min). Rolling back to previous TLS. Rollback complete at 2025-01-30T01:45:33Z. Halting remaining upgrades after multiple rollbacks.


## Example Final Summary (Excerpt)

- checkout-api: target 1.2 | baseline 1,245/min | post-change stable | anomaly: no | rollback: no | final TLS: 1.2
- billing-service: target 1.2 | baseline 820/min | post-change 0/min | anomaly: yes | rollback: yes | final TLS: previous version

Plan status: Partial completion – halted after repeated anomalies.
