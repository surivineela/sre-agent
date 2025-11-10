# AKS Workload Deep Diagnosis

Supplementary deep-diagnosis reference within the AKS operations skill. Provides structured hypothesis testing for ambiguous or multi-signal workload issues.

If primary symptom is networking / connectivity → open `aks_network_remediation.md` instead.

## Layered Model (What & Why Only)

| Layer | Observability Focus | Change History Focus | Typical Outputs |
|-------|---------------------|----------------------|-----------------|
| Application | Container states, probe results, logs (current/previous), restarts | Image tag/digest changes, probe spec changes, env/config updates | Failure mode classification |
| Infrastructure (K8s-visible) | Node conditions, scheduling events, CNI-related pod events, image pulls | DaemonSet / admission controller updates | Systemic vs workload-local determination |
| Dependency | Service↔Endpoint mapping, DNS resolution outcomes, connection errors/timeouts | Upstream workload rollouts, Ingress changes | Upstream regression vs local issue |

## Diagnostic Loop

1. Frame: workload (ns/kind/name), timeframe, symptom summary, business impact.
2. Baseline Snapshot (atomic pull): status + events + current/prev logs + rollout history + endpoints.
3. Signal Grouping: bucket observations by layer; note contradictions.
4. Hypothesis Set: 2–4 plausible causes only. Discard low-probability early.
5. Evidence Test: for each hypothesis list 1–2 confirming + 1–2 falsifying signals → gather ONLY missing ones.
6. Converge: eliminate until single cause (or ranked list with evidence deltas).
7. Remediation Recommendation: minimal action or required escalation (e.g., code fix, config revert, upstream change). Provide rollback.
8. Verification Sample: post-change quick re‑snapshot (same fields as baseline) → show delta table.

Stop if: further evidence would duplicate existing signals or remaining hypotheses indistinguishable without external (out‑of‑scope) data — report best-supported cause + uncertainty note.

## Evidence Set (Compact)

| Category | Key Fields | Purpose |
|----------|-----------|---------|
| Workload Describe | conditions, replicas, progress, failures | Deployment health trajectory |
| Pod Describe | container state (waiting/terminated reasons), restart counts | Failure mode fingerprint |
| Logs (curr/prev) | startup stack traces, error patterns, OOM/panic | Application-level trigger |
| Rollout History | ReplicaSets / ControllerRevisions ages | Correlate change vs symptom onset |
| Service/Endpoints | selector alignment, endpoint readiness | Dependency mapping |
| Resource Metrics (if relevant) | CPU throttle, memory RSS vs limits | Resource pressure validation |

Collect once per phase; avoid repetitive full re-fetches—target deltas.

## Hypothesis Template

```text
Hypothesis: <statement>
Confirms: <signal1>, <signal2>
Falsifies: <signal3>, <signal4>
Status: (Pending | Confirmed | Rejected) + brief note
```

Maintain concise list; remove rejected immediately to reduce cognitive load.

## Classification Quick Hints

| Pattern | Likely Class | Discriminators |
|---------|-------------|---------------|
| Immediate crash on start + new image | Bad release | Previous ReplicaSet stable |
| Readiness flaps + steady restarts low | Dependency latency / probe config | Logs show timeouts; no crash traces |
| Slow ramp + ProgressDeadlineExceeded | Rollout failure | Events show failed progress |
| OOMKilled after steady growth | Memory leak / undersized limits | Logs lack fatal trace; memory RSS trend |
| Throttle + latency spike | CPU saturation | High throttling metrics + no crash |

## Producing the Root Cause Statement

Format:

```text
Root Cause: <concise plain-language description>
Evidence: <3–5 bullet signals>
Remediation: <minimal action + rollback>
Residual Risk: <if any>
```

## Error / Permission Handling

Summarize (identity, missing permission, needed role) — no raw dump. Ask user if they can grant; retry after confirmation. Do not fabricate role commands.

## When to Open Other Supplementary Files

| Situation | Open File |
|-----------|----------|
| Network-only failures (DNS, ingress, egress, NetworkPolicy) | aks_network_remediation.md |
| Need to apply concrete change | aks_remediation.md |
| Need patch/scale/label syntax & safety | kubectl_command_executor.md |
| Persistent CPU or memory anomaly after baseline | diagnostic_cpu / diagnostic_memory |

## Output Expectations

Answer start: cause (or ranked causes). Then evidence table or bullets. Then recommended minimal remediation (or next data needed). Keep internal hypothesis reasoning hidden unless user requests.

## Example (Abbreviated)

Baseline: 5 desired / 3 ready, events show ImagePullBackOff, logs: 404 pulling image tag v2.1.3.
Hypotheses: (1) Bad image tag (2) Registry auth issue. Evidence: other workloads pull from same registry successfully → reject (2). Confirm (1).
Remediation: rollback to previous ReplicaSet (v2.1.2). Verification: 5/5 ready, no new events. Root cause reported.

---

Conclude only when: single supported cause OR clearly stated uncertainty with narrowed options + explicit next investigative gap.
