# Azure Function App Execution Failures

## Overview
Diagnose and resolve execution failures in Azure Functions by analyzing failed invocations, exception patterns, stack traces, and deployment timing. Correlate logs, metrics, and configuration to identify root causes and propose actionable fixes. When indicated, consult related files for configuration and deployment specifics, and leverage performance-focused skills for CPU and memory-related failures.

## Planning Checklist
- Define scope: app name, region, timeframe (typically past 24 hours), and impacted functions.
- Gather signals: failed invocation metrics, exception summaries, detailed stack traces, and recent deployments/slot swaps.
- Classify failures: configuration, code, dependency/connectivity, resource constraints, or deployment timing.
- Determine confidence level based on exception consistency, timing correlations, and clarity of evidence.
- Outline remediation path: configuration changes, code fixes, rollout/rollback, or resource adjustments.

## Confidence Assessment Framework
- High (>80%):
  - Consistent exception patterns affecting >50% of failures
  - Clear correlation between deployment timing and failure onset
  - Well-known exception types with obvious fixes (timeouts, connection failures)
  - Stack traces pinpoint specific code lines with identifiable issues
- Medium (50–80%):
  - Exception patterns affecting 20–50% of failures
  - Multiple corroborating data sources (logs, metrics, timing)
  - Clear configuration issues aligned with best practices
  - Resource constraints visible in performance metrics
- Low (<50%):
  - Scattered exception patterns and no clear majority
  - Limited diagnostics or insufficient timeframe
  - Complex or obscure exception types
  - Conflicting evidence across sources

## Solution Categorization
- Immediate Fixes: Configuration changes, resource scaling, deployment rollback.
- Code Fixes: Programming errors indicated by stack traces and exception messages.
- Investigation Required: Complex issues requiring deeper development analysis.
- Documentation Updates: Process and runbook improvements informed by findings.

## Diagnostic Workflow
1. Failed Invocations Overview
   - Retrieve failed function invocations for the targeted period (default 24 hours).
   - Produce a time series chart titled "Function Invocation Failures".
   - Highlight the average failure rate in bold and summarize health implications.

2. Exception Pattern Analysis
   - Identify top exceptions per function (focus on top 3 for each).
   - Build a bar chart titled "Top Exceptions per Function" with exception type on the x-axis and count on the y-axis.
   - Explain the dominant patterns and outline next diagnostic steps based on findings.

3. Detailed Exception Investigation
   - Retrieve full exception details and complete stack traces for top exceptions.
   - Present exceptions and stack traces in bulleted form without truncation.
   - For each exception, infer likely causes (e.g., dependency timeouts, null references, serialization errors, binding issues) and map to remediation categories.

4. No Exceptions or Sparse Data
   - If exception data is missing or sparse, validate logging configuration and failure telemetry coverage.
   - Investigate trigger/binding misconfigurations, identity, and app settings. Consult [function_app_configuration_checker.md](function_app_configuration_checker.md).

5. Deployment and Slot Swap Correlation
   - Review deployment and slot swap history; emphasize successful swaps during the failure window.
   - If a successful swap aligns with failure onset, issue a clear warning and assess rollback feasibility.
   - When deployment artifacts, start-up behavior, or runtime changes are implicated, consult [function_app_deployment_checker.md](function_app_deployment_checker.md).

6. Rollback and Validation (When Timing Correlates)
   - Propose rollback to the last known good version if evidence indicates deployment-induced failures.
   - After rollback, monitor failed invocations for at least 5 minutes, updating the failure time series.
   - Compare pre/post rollback failure rates and report improvement or continued impact.

7. Resource Constraint Assessment
   - If exceptions indicate timeouts, thread starvation, or out-of-memory conditions, or if failure rates correlate with load, evaluate CPU and memory.
   - Use the performance-focused skills:
     - For memory spikes, leaks, or OutOfMemoryException, see the "diagnostic_memory" skill.
     - For CPU spikes, saturation, or high CPU, see the "diagnostic_cpu" skill.
   - Translate performance findings into concrete mitigations (scaling, concurrency limits, connection pooling, caching).

8. Solution Assessment and Decision
   - Assign confidence level (High/Medium/Low) with rationale.
   - When Medium or High:
     - Propose immediate configuration fixes (timeouts, retry policies, connection strings, identity).
     - Propose code fixes (null checks, async patterns, resilient clients, input validation) referencing stack trace locations.
     - Recommend deployment actions (rollback, hotfix, slot warm-up procedures).
     - Recommend resource/scaling changes if performance-bound.
   - When Low:
     - Document findings comprehensively and escalate for deeper analysis.

9. Implementation Support
   - For configuration fixes: specify exact keys/values and expected impact.
   - For code fixes: cite implicated function(s) and suspected line(s) from traces; provide suggested code-level remediation where straightforward.
   - For deployment: outline rollback/forward steps and post-change monitoring expectations.

10. Documentation and Tracking
   - Create a thorough issue/ticket when needed, including:
     - Title: Function App Execution Failures: [App Name] - [Primary Exception Type]
     - Summary of failure patterns and impact
     - Diagnostic findings (failure rate/time period, exception counts, full stack traces)
     - Deployment history within failure window
     - Configuration analysis results
     - Confidence assessment and reasoning
     - Proposed solutions and expected impact
     - Supporting evidence (charts, logs)
     - Next steps for the development team

## Instructions and Best Practices
- Always correlate exception spikes with deployment timestamps before attributing root cause.
- Treat recurring exceptions with identical stack frames as prime candidates for immediate code fixes.
- Distinguish between transient dependency failures (use retries, circuit breakers, timeouts) and persistent misconfigurations (fix settings, identities, endpoints).
- For HTTP-triggered functions, separate client-induced 4xx errors from server-side 5xx failures; prioritize server-side issues.
- Validate binding and trigger configurations for queue, blob, Event Grid, and Service Bus functions when exceptions reference binding failures.
- For dependency timeouts:
  - Tune client timeouts and retry policies.
  - Ensure connection pooling and reuse; avoid socket exhaustion.
  - Verify DNS resolution, firewall, and private endpoints as applicable.
- For performance-related failures:
  - Inspect concurrency, scaling settings, and function execution duration distribution.
  - Use the "diagnostic_memory" and "diagnostic_cpu" skills when exceptions or metrics implicate memory or CPU constraints.

## Output Requirements
- Use charts for metrics and trend data (failure rates over time; exception counts).
- Present complete exceptions and stack traces without truncation.
- Clearly separate successful analysis steps from error conditions.
- When describing deployment timelines, include tables for relevant events (Timestamp, Operation, Caller) and distinctly call out successful swaps during the failure window.

## Error Handling
- Report only permission-related errors that block analysis and specify the impacted scope.
- Use alternative diagnostics if certain data sources are unavailable; do not disclose underlying platform/tool constraints.
- Provide evidence-backed conclusions; avoid speculation beyond available data.

## Related Files and Skills
- Configuration validation and bindings (including Blob + Event Grid): [function_app_configuration_checker.md](function_app_configuration_checker.md)
- Deployment checks, swaps, and artifact validation: [function_app_deployment_checker.md](function_app_deployment_checker.md)
- Performance diagnostics (memory): activate the "diagnostic_memory" skill when observing OutOfMemoryException, memory leaks, large spikes, or user-requested memory analysis.
- Performance diagnostics (CPU): activate the "diagnostic_cpu" skill when analyzing CPU spikes, high CPU saturation, or suspected compute bottlenecks.

## Examples
- Consistent SqlException timeout after a swap:
  - Failure spike begins within minutes of a successful slot swap.
  - Stack traces point to database calls with default timeouts.
  - Action: rollback swap; increase command timeout and implement retry policy; validate improvement via updated failure time series.
- Intermittent NullReferenceException in a single function:
  - Top exception count dominates for that function with identical stack frame.
  - Action: add null checks, improve input validation, and add defensive logging; confirm reduction in exception count post-deploy.
- OutOfMemoryException during peak load:
  - Failure rates correlate with load; memory exceptions appear in traces.
  - Action: use the "diagnostic_memory" skill; recommend scaling, limit parallelism, optimize allocations, and monitor post-change failure rates.
