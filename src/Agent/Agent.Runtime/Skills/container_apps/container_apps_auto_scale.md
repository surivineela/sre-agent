# Azure Container Apps Auto-scaling (KEDA)

## Overview
Provide authoritative guidance for configuring, validating, and troubleshooting Azure Container Apps horizontal auto-scaling driven by KEDA and platform HTTP scaling. Diagnose misconfigurations, interpret scaling decisions using metrics and logs, convert KEDA trigger definitions into Container Apps scale rules, and apply safe remediations. Use clear, step-by-step procedures and validate each change.

## Capabilities
- Diagnose and resolve auto-scaling failures (no scale-out, delayed scale-in/out, oscillations, unexpected replica counts).
- Configure KEDA-based scale rules and platform HTTP scaling (min/max replicas, rules, polling and cooldown behavior).
- Interpret scaling signals and decisions from metrics, scaler logs, and rule status.
- Convert existing KEDA trigger YAML to Container Apps scale rule format.
- Provide actionable remediation with Azure CLI commands and safe rollout guidance.

## When to Use Related Resources
- Latency analysis beyond scaling: Read [diagnostic_latency.md](diagnostic_latency.md) when latency remains high despite adequate replicas, or when network- or dependency-induced delays are suspected.
- CPU analysis: Use the diagnostic_cpu skill for CPU spikes, bottlenecks, or mismatches between CPU saturation and scaling behavior.
- Memory analysis: Use the diagnostic_memory skill for memory leaks, OOM events, or memory-driven instability affecting scaler behavior.
- Metrics visualization and anomaly detection: Use the metrics_and_chart_visualization skill to graph scaling signals, replica counts, and trigger metrics; prepare resource IDs and clear goals before activation.

## Diagnostic Workflow
1. Clarify the scaling objective
   - Trigger type (HTTP concurrency, CPU, queue length, events per second, cron, custom).
   - Expected target behavior (replica range, response times, backlog targets).
   - Time window and traffic pattern (bursty vs steady, day-parting).

2. Collect current configuration and state
   - App and environment: az containerapp show -g <rg> -n <app>
   - Revisions and replica counts: az containerapp revision list -g <rg> -n <app>
   - Scale block: properties.template.scale (minReplicas, maxReplicas, rules).
   - For HTTP scaling: scale.http.concurrency or http scale rule.
   - Identity and permissions for cloud triggers (Service Bus, Storage, Event Hubs, etc.).

3. Validate rule integrity
   - Rule name uniqueness, correct scaler type, required metadata parameters.
   - Authentication parameters (connection strings, secret refs, MSI identity).
   - Reasonable target values and polling/cooldown windows.
   - Min/max replica bounds allow required headroom.

4. Examine logs and metrics
   - App logs for throughput/backlog signals.
   - System logs for scaling decisions when available.
   - Metrics: replica count, trigger metric value (e.g., queue length), CPU/memory utilization, HTTP concurrency/requests.
   - Visualize trends and correlations with the metrics_and_chart_visualization skill if needed.

5. Identify root cause patterns
   - No scale-out despite load: missing permissions/secret, wrong trigger name/namespace, incorrect target value, too low maxReplicas, long pollingInterval or high stabilization window, throttling at dependency.
   - Oscillation: overly aggressive targets, too small cooldownPeriod, noisy metrics, small minReplicas, traffic bursts vs polling cadence mismatch.
   - Delayed scale-in/out: long stabilization window or cooldown, slow metric propagation, dependency lag.

6. Remediate safely
   - Adjust min/maxReplicas to permit expected range.
   - Correct rule metadata and authentication.
   - Tune pollingInterval, cooldownPeriod, and stabilizationWindow.
   - Update target values to align with SLOs (e.g., queue length per worker).
   - Validate after each change and monitor for 1–2 scaling cycles.

7. Verify and summarize
   - Confirm expected replica behavior under realistic load.
   - Document configuration changes, rationale, and next steps.
   - Provide rollback details if changes are not effective.

## Configuration Reference

### Key Scale Properties
- minReplicas: Lower bound of replicas. Set ≥1 if cold starts are unacceptable.
- maxReplicas: Upper bound; ensure sufficient headroom for peak load.
- rules: KEDA trigger definitions. Use correct scaler type and metadata.
- pollingInterval: Seconds between metric polls (default varies by scaler).
- cooldownPeriod: Seconds to wait before scaling down after last trigger.
- stabilizationWindow: Minimum duration for stable metric before scale decision.

### Common KEDA Scalers (Examples)
- CPU/Memory: Targets utilization percentage per replica.
- Azure Service Bus: queueLength or messageCount across queues/subscriptions.
- Azure Storage Queue: queueLength.
- Event Hubs/Kafka: eventsPerSecond, lag thresholds.
- Cron: Scheduled minReplicas changes.
- Prometheus/HTTP: Metric threshold or concurrency target.
- Custom: External Push/metrics adapter via custom scaler.

### Example: CPU-based Autoscaling (YAML)
- minReplicas: 2
- maxReplicas: 10
- rules:
  - name: cpu-scale
    custom:
      type: cpu
      metadata:
        type: Utilization
        value: "70"
      pollingInterval: "15"
      cooldownPeriod: "120"

Notes:
- Some environments use dedicated CPU scalers; confirm supported schema by platform version.
- For CPU/Memory, prefer platform-native configuration when available.

### Example: Azure Service Bus Queue Scaling (YAML)
- minReplicas: 1
- maxReplicas: 20
- rules:
  - name: sbq
    azureServiceBusQueue:
      queueName: orders
      messageCount: "50"
      connectionFromEnv: SERVICEBUS_CONN
      pollingInterval: "15"
      cooldownPeriod: "180"

Prerequisites:
- SERVICEBUS_CONN must be a valid connection string secret or identity-based auth configured.
- Ensure app identity has receive and management permissions if using MSI.

### Example: HTTP Concurrency Scaling (YAML)
scale:
  http:
    concurrency: 50
  minReplicas: 2
  maxReplicas: 30

Notes:
- HTTP concurrency defines target in-flight requests per replica. Validate against latency SLOs.

## Azure CLI Patterns

### Retrieve and Inspect
- Show app configuration:
  az containerapp show -g <rg> -n <app> -o jsonc
- List revisions and replica counts:
  az containerapp revision list -g <rg> -n <app> -o table
- Stream logs (app/system):
  az containerapp logs show -g <rg> -n <app> --type app --follow
  az containerapp logs show -g <rg> -n <app> --type system --follow

### Update Min/Max Replicas
- Set min/max:
  az containerapp update -g <rg> -n <app> --min-replicas 2 --max-replicas 20

### Add or Update a Scale Rule
- Add/update a Service Bus queue rule:
  az containerapp update -g <rg> -n <app> \
    --scale-rule-name sbq \
    --scale-rule-type azure-servicebus-queue \
    --scale-rule-metadata queueName=orders messageCount=50 \
    --scale-rule-auth connection=SERVICEBUS_CONN

- Set polling and cooldown (if supported via args; otherwise use --set):
  az containerapp update -g <rg> -n <app> \
    --set properties.template.scale.rules[0].custom.metadata.pollingInterval="15" \
          properties.template.scale.rules[0].custom.metadata.cooldownPeriod="180"

### Configure HTTP Concurrency
- Update HTTP concurrency:
  az containerapp update -g <rg> -n <app> \
    --set properties.template.scale.http.concurrency=50

Notes:
- CLI flags evolve; if a direct flag is unavailable, use --set to patch the ARM path.
- After updates, validate with az containerapp show and a short load test.

## Tuning Guidelines and Best Practices
- Start conservative: Set minReplicas to ensure baseline responsiveness; avoid frequent cold starts for interactive workloads.
- Right-size targets: Translate SLOs (e.g., p95 latency, backlog minutes) into scaler thresholds (e.g., queue length per replica).
- Balance responsiveness vs stability:
  - Short pollingInterval improves response to bursts but may cause oscillations.
  - Longer cooldownPeriod and stabilizationWindow reduce flapping.
- Avoid hard caps during peak: Ensure maxReplicas accommodates peak plus buffer; monitor cost impact separately.
- Ensure auth and connectivity: Verify secrets, identity assignments, and network access to metric sources.
- Version control scale config: Track YAML or ARM Bicep for reproducibility and rollback.
- Validate under realistic load: Use representative traffic or replay to confirm behavior.
- Observe end-to-end: Scaling won’t fix upstream dependency limits; correlate with downstream metrics.

## Common Issues and Remediation

- Symptom: No scale-out with growing queue.
  - Check: maxReplicas too low; missing/invalid connection; wrong queue/topic name; insufficient permissions.
  - Fix: Raise maxReplicas; correct metadata; set or rotate connection secret; assign proper RBAC for MSI.

- Symptom: Rapid scale oscillation.
  - Check: Very low target threshold; short cooldownPeriod; noisy metric; bursty traffic with short polling.
  - Fix: Increase cooldown/stabilization; raise target; smooth metric via higher target aggregation window if supported.

- Symptom: Slow scale-in after spike ends.
  - Check: Long stabilizationWindow or cooldownPeriod; metric lag.
  - Fix: Reduce cooldown/stabilization to desired recovery time.

- Symptom: High latency despite expected replicas.
  - Next step: Read [diagnostic_latency.md](diagnostic_latency.md); also check CPU and memory with diagnostic_cpu and diagnostic_memory skills.

## Converting KEDA to Container Apps Scale Rules
- Map trigger type: Ensure scaler type matches Container Apps schema (e.g., azureServiceBusQueue vs custom).
- Translate metadata: Copy keys (queueName, messageCount, topic, subscription, lagThreshold, etc.) exactly; string values required for many fields.
- Auth section:
  - connectionFromEnv for secret-based.
  - identity-based auth requires appropriate identity and trigger support.
- Advanced timings: Map pollingInterval, cooldownPeriod, and stabilizationWindow when supported by the scaler in Container Apps.
- Validate after conversion with a controlled load to confirm parity.

## Validation After Changes
- Confirm configuration applied:
  - az containerapp show -g <rg> -n <app> | jq '.properties.template.scale'
- Induce or observe load representative of target scenario.
- Monitor:
  - Replica count trend vs trigger metric and latency.
  - Any scaler errors or authentication failures in system logs.
- Success criteria examples:
  - Queue backlog stabilizes within target minutes.
  - p95 latency within SLO with stable replicas.
  - No oscillation beyond agreed bounds in a 30–60 minute window.

## Safe Rollout and Rollback
- Apply changes in small increments; prefer single-variable changes per iteration.
- Document current settings before change; keep a rollback block ready.
- If behavior degrades, rollback immediately and reassess targets, timings, and dependencies.

## Example Remediation Report
- issue_summary: Container app not scaling out under peak traffic.
- observed_behavior: Queue length >10k, replicas capped at 3.
- findings:
  - maxReplicas set to 3.
  - SERVICEBUS_CONN secret missing in environment.
  - poll interval 60s causing delayed reaction to bursts.
- root_cause: Misconfigured maxReplicas and missing connection secret.
- actions_taken:
  - Set maxReplicas to 20.
  - Added SERVICEBUS_CONN secret and referenced in scale rule.
  - Reduced pollingInterval to 15s; cooldownPeriod to 180s.
- verification:
  - Under test load, replicas scaled from 2 to 12 in 2 minutes.
  - Queue drained from 10k to <500 in 6 minutes; p95 latency within SLO.
- next_steps:
  - Monitor for 24h; revisit targets if traffic increases 2x.
  - Add alerts for queue backlog and replica saturation.
  - Visualize trends with the metrics_and_chart_visualization skill.

## Related Resources
- Latency investigations: [diagnostic_latency.md](diagnostic_latency.md)
- CPU analysis: diagnostic_cpu skill
- Memory analysis: diagnostic_memory skill
- Metrics visualization and anomaly detection: metrics_and_chart_visualization skill
