# Azure Application Insights Skill

Use this skill to perform structured, telemetry-driven analysis of application health, reliability, performance, and user experience using Azure Application Insights.

The agent should follow the steps below and adapt them to the specific incident, time window, and business context provided in the conversation.

## Core Capabilities

- Health assessment: Summarize overall application health using key metrics.
- Exception and error analysis: Identify failure patterns and likely root causes.
- Performance analysis: Examine latency distributions, trends, throughput, and bottlenecks.
- Dependency health: Evaluate external services for failures, timeouts, and resilience gaps.
- User behavior analysis: Describe user impact, experience degradation, and usage patterns.
- Request tracing: Perform end-to-end trace analysis with correlation IDs where available.
- Actionable recommendations: Provide specific, prioritized remediation and improvement steps.

## Analysis Workflow

### 1. Confirm Inputs

Before using tools, confirm or infer:

- Application Insights resource(s) to analyze.
- Time range for investigation (default to last 24 hours if not specified).
- Incident description and business context (user impact, critical flows, SLA concerns).
- Any known correlation IDs, operation names, or key endpoints.

If key details are missing but important for precision (for example, time range or critical transaction name), briefly call them out and proceed with reasonable assumptions.

### 2. Identify Anomalies (CorrelateTimeSeries)

Use `CorrelateTimeSeries` as the primary entry point for telemetry analysis.

When calling `CorrelateTimeSeries`:

- Focus on relevant metrics such as request duration, failed requests, dependency duration, and exceptions.
- Look for spikes, sustained degradations, drops in throughput, or baseline shifts.
- Pay attention to dimensions such as `operationName`, `cloudRoleName`, `dependencyType`, `resultCode`, `exceptionType`, and user/location attributes.

Summarize the most important anomalies and form initial hypotheses about possible causes.

### 3. Quantify Impact (GetImpact)

Use `GetImpact` after anomalies are identified to quantify how serious they are.

When calling `GetImpact`, aim to determine:

- Number and percentage of affected requests.
- Number of unique users impacted.
- Changes in error rate (absolute and relative).
- Throughput degradation.
- Impact on critical transactions (for example, login, checkout, or other business-critical flows).

Use these results to prioritize which problems to analyze in depth.

### 4. Inspect Traces (ListDistributedTraces, GetDistributedTrace)

Use trace tools only after there is a clear focus area from correlation and impact analysis.

- Use `ListDistributedTraces` to list traces for the relevant operations, time windows, and failure modes.
- From the list, select representative traces (for example, slowest traces, failed traces, or traces from critical operations).
- Use `GetDistributedTrace` to inspect those traces in detail.

From trace details, capture:

- The slowest spans and where time is spent.
- Failing spans, including status codes and exception messages.
- Dependency call ordering and timing.
- Retry, timeout, or circuit-breaker behavior if present.

Connect this trace evidence back to the anomalies and impact you previously identified.

### 5. Validate Findings

Combine insights from metrics, impact, and traces to validate or refine hypotheses.

Check that:

- Time-series anomalies line up with trace-level latency or error spikes.
- Dependency failures or slowdowns align with primary operation degradation.
- Exception types and patterns support the proposed root cause.

If needed, use additional targeted `CorrelateTimeSeries` calls to confirm whether specific error signatures, endpoints, or dependencies are strongly associated with observed impact.

### 6. Present Results

Present results in a concise, structured format that balances technical detail with clarity for stakeholders.

## Output Structure (Required)

Use the following structure when summarizing an Application Insights investigation:

1. Executive Summary
   - 2–3 sentences summarizing overall health and the most important issues.

2. Key Findings
   - 3–5 prioritized findings, ordered by business or user impact.

3. Detailed Analysis (per finding)
   - Baseline vs current metrics (for example, average, P50, P95, P99).
   - Time window(s) and timeline description.
   - Affected dimensions (operations, dependencies, regions, user segments).
   - Supporting evidence from traces, exceptions, and dependencies.

4. Immediate Actions (0–24 hours)
   - Concrete mitigation steps such as rollbacks, scaling changes, configuration fixes, or temporary feature flags.

5. Medium-Term Recommendations (next few days to a week)
   - Performance tuning, resilience improvements, or dependency optimization.

6. Long-Term Strategy
   - Architectural changes, observability improvements, capacity planning, and resilience practices.

## Types of Insights to Look For

- **Performance bottlenecks**: Elevated percentile latencies, queue buildup, or resource saturation.
- **Reliability issues**: Error rate spikes, exception concentration, or failure patterns.
- **Dependency problems**: Timeouts, throttling, or error clusters from external or internal services.
- **User experience issues**: Slow user-facing operations, increased client errors, or conversion/engagement drops.

## Input Context Handling

When interpreting telemetry:

- Keep analysis scoped to the specified Application Insights resource(s), calling out any multi-role or multi-region complexity.
- Segment analysis when issues vary by time window (for example, peak vs baseline, or intermittent patterns).
- Use correlation IDs when available to follow problematic transactions across services.
- Tie each major finding back to user impact, revenue risk, SLA breach risk, or critical business workflows.

## Tool Usage Summary

- `CorrelateTimeSeries`: Primary tool to detect anomalous dimensions and time windows for key metrics.
- `GetImpact`: Quantifies breadth and severity once anomalies are identified.
- `ListDistributedTraces`: Enumerates relevant traces after you know which operations, windows, or failure modes to target.
- `GetDistributedTrace`: Provides detailed, span-level insight for selected traces.

Use tools in combinations that support clear, end-to-end reasoning rather than issuing large numbers of unstructured calls.

## Analysis Best Practices

- Be specific with numbers (for example, "P95 latency increased from 420 ms to 980 ms; error rate from 0.8% to 6.2%").
- Prioritize issues based on user and business impact.
- Explain how technical symptoms translate into user experience (for example, checkout abandonment, login failures).
- For each finding, include at least one realistic remediation path (for example, code fix, configuration change, scaling, resilience pattern, caching, or retry adjustments).
- Consider dependencies explicitly when attributing performance or reliability issues.
- Describe the overall picture rather than focusing on isolated metrics.

## Evidence to Capture Per Finding

For each major finding, aim to capture:

- Baseline vs impacted metrics (average and key percentiles).
- Error and exception counts, and top exception types.
- Dependency success vs failure ratios and relevant latency metrics.
- Signs of retries, timeouts, throttling, or saturation.
- Throughput changes (for example, requests per second).
- Approximate number of affected users or sessions.
- One or more representative trace IDs and what they show about execution flow.

## Structuring Recommendations

When suggesting remediation:

- **Immediate**: Actions that reduce impact now (for example, rollback, scaling, configuration fixes, feature flags, temporary fallbacks).
- **Medium term**: Code and configuration changes for performance, resilience, or dependency efficiency.
- **Long term**: Architectural and observability improvements, capacity and chaos testing, and resilience design patterns.

Use this skill to generate consistent, evidence-backed Application Insights analyses that help stakeholders quickly understand issues and decide on mitigations and improvements.
