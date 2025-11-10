# Azure Web App Restart Investigation Skill

Focused diagnostic guidance for unexpected Azure Web App (App Service) restarts, crash loops, and instability after deployments or configuration changes.

## 1. Scope & Goals

Identify the primary driver of restart events (deployment, configuration, resource pressure, dependency failure, unhandled exception, scaling behavior) and produce either:

1. A confirmed root cause with evidence, or
2. Ranked hypotheses (each with confidence, evidence, and next validation step).

## 2. Investigation Workflow (do not narrate every internal step to the user; respond concisely per system prompt)

1. Gather restart execution data (time stamps, trigger types, failure indicators).
2. Build a restart timeline (±2h from first to last restart). Use bar chart representation; label bars as Restart.
3. Correlate timeline with:
   - Exceptions & stack traces during restart windows.
   - Failed requests and upstream dependency latency/errors.
   - Deployment & slot swap activity.
   - Configuration changes or scaling events.
   - Resource utilization (CPU, memory) trends.
4. Identify candidate cause patterns (see Section 5).
5. Validate strongest candidates (look for repeatability, temporal alignment, exclusivity).
6. Produce concise answer first, then supporting evidence blocks.

## 3. Data Collection Rules

Time Window: From (first restart - 2h) to (last restart + 2h). If only one restart, still apply ±2h.

Timezone: Normalize all timestamps to UTC and state it once.

Relevance Filter: Exclude data outside the window unless required to prove stability before/after a fix.

## 4. Presentation Structure

Answer (1–2 sentences) → Restart timeline → Key metrics (time series) → Correlated exceptions (only those temporally aligned; full stack traces for correlated ones) → Deployment table → Recommendations → Next validation steps.

## 5. Pattern Recognition Cheatsheet

Look for:

- Deployment-triggered loops (restart immediately or within minutes of slot swap).
- Memory/resource pressure (rising memory or CPU plateau preceding restart).
- Dependency failures causing process termination (recurring timeout → unhandled exception → restart).
- Configuration changes causing misbinding (setting applied then errors then restart).
- Continuous crash on cold start (same exception every restart within short interval).

## 6. Exception & Stack Trace Handling

Include full message + stack only for exceptions occurring within the restart correlation window or clearly precipitating the restart. Summarize counts/types for others. Highlight frames that reference application code (exclude framework noise unless critical). Classify exceptions: transient vs structural (e.g., NullReference vs Timeout vs OutOfMemory).

## 7. Deployment & Slot Activity

Produce a table (Timestamp | Operation | Source Slot | Target Slot | Result). Only list successful operations inside the correlation window or immediately preceding the first restart (<30m). Note if a deployment coincides with config changes.

## 8. Recommendations Format

Each recommendation: Action | Rationale | Expected Impact | Validation Metric. Prioritize by ease + expected risk reduction. Include rollback/safety note for any configuration/code change suggestion.

## 9. Completion Criteria

Stop using this skill when a root cause (or highest-confidence hypothesis + next step) is delivered. If investigation scope shifts away from restarts (e.g., general performance tuning), conclude and proceed without reloading. Do not continue cycling once evidence is exhausted—present remaining hypotheses transparently.

## 10. Error Handling (Internal Guidance)

If a data retrieval fails, retry with narrower scope or alternative visualization. Only surface permission/auth issues to the user. Do not mention internal tool names in user responses. Gracefully proceed with partial data rather than emphasizing absence.

## 11. Tool Utilization (Internal Only)

Use restart execution retrieval first; then chart time series (CPU, memory, request rate, error rate) and bar chart for restarts. Parallelize independent read operations after resource context is confirmed. Avoid re-requesting identical time spans unless new data is expected.

## 12. Evidence Quality Checklist

Before finalizing:

- Restart timeline accurate & complete.
- All timestamps normalized to UTC.
- Direct temporal alignment shown for chosen cause.
- Exceptions included only if causally relevant.
- Deployment/config influence assessed.
- Recommendations actionable & mapped to evidence.

## 13. Minimal Example Output (Illustrative)

Answer: The repeated restarts are most likely caused by an unhandled OutOfMemory exception triggered by sustained memory growth after the last deployment.

Supporting:

- Timeline: 5 restarts between 12:10–12:55 UTC (chart)
- Memory: Steady climb from 65%→92% preceding each restart (line chart)
- Exceptions: 5 x OutOfMemoryException (full stacks) within 30s of each restart
- Deployment: Slot swap at 11:55 UTC introducing higher memory footprint
- Recommendation: Add memory profiling + implement streaming for large payload processing; scale plan tier temporarily to validate stabilization.

Use this structure as a guide; adapt content to actual evidence.
