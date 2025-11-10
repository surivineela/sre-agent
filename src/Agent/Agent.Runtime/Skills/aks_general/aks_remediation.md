# AKS Remediation

Supplementary remediation reference within the single AKS operations skill. Provides patterns for applying safe, reversible changes to restore workload health.

### Scope

| In Scope | Out of Scope |
|----------|--------------|
| Deployments, StatefulSets, DaemonSets, Jobs/CronJobs | Node pool scaling, cluster provisioning |
| Pods, Services, Ingress, ConfigMaps, Secrets, HPAs, NetworkPolicies | Application source code review |
| Rolling back failed rollouts, adjusting probes, tuning resources | Destructive deletions |

If signals conflict or root cause uncertain → open `aks_workload_diagnose.md`.

### Remediation Loop (Builds on Core Pattern)

- (1) Confirm target: namespace, kind, name (single workload). Disambiguate first.
- (2) Collect focused evidence (only what supports a change):
  - Workload + pod describe (conditions, events)
  - Current + previous container logs (error patterns)
  - Rollout / ReplicaSet history (recent changes)
- (3) Classify category: rollout failure | config/probe | resource pressure | dependency/network (open network supplementary file) | crash loop
- (4) Propose minimal change (rollback, probe tweak, resource adjustment, label/annotation fix, env/config correction) + rollback
- (5) Confirm (if write): change + impact + rollback in ≤3 lines
- (6) Execute one change
- (7) Verify: readiness, restarts delta, events quieting, logs clear
- (8) Decide: additional change or finalize

### Evidence Shortlist

- Status & Events: `ImagePullBackOff`, `CrashLoopBackOff`, `Unhealthy`, `ProgressDeadlineExceeded`
- Logs: startup exceptions, OOM / panic, connection timeouts
- Replica / Revision: last working ReplicaSet (age vs symptom onset)
- Resource Metrics: only if change relates to sizing (CPU throttle, OOMKilled)

### Remediation Actions (Patterns)

| Situation | Action | Rollback |
|-----------|--------|----------|
| Recent bad deployment | Rollback to previous ReplicaSet | Re-roll forward after fix |
| Probe misconfiguration | Adjust path / port / thresholds | Revert to prior probe spec |
| CPU saturation (evidence) | Increase requests/limits or scale replicas | Restore prior values |
| Memory OOM | Raise limits (with justification) or optimize usage | Prior limits (if regression) |
| ConfigMap/Secret key missing | Correct reference / update config | Reapply previous manifest |
| Crash on new image | Rollback image tag | Redeploy fixed image |

Record every applied change via `change_propagation.md` (even temporary mitigations) to avoid drift.

### Permission / Access Errors

Summarize (identity, needed role) — no raw dump. Ask for confirmation after fix, then retry collection or remediation step.

### Example – CrashLoop After Rollout

1. Evidence: new ReplicaSet timestamp aligns with restarts; logs show missing env variable.
2. Action Proposal: rollback to previous revision; impact = restore stable version; rollback = redeploy fixed image later.
3. Execute rollback, verify stable readiness & restarts plateau.
4. Record change (rollback) + open issue (missing env mapping) if needed.

### Open Other Supplementary Files

| If You Observe | Open File |
|----------------|----------|
| DNS failures / ingress pending / NetworkPolicy denials | aks_network_remediation.md |
| Multiple contradictory signals; no single dominant failure | aks_workload_diagnose.md |
| Need patch / scale / label syntax & safety details | kubectl_command_executor.md |

### Related Top-Level Diagnostic Skills

- `diagnostic_cpu` for persistent CPU saturation / throttling after initial remediation.
- `diagnostic_memory` for OOMKilled or abnormal growth patterns.

### Output Expectations

Final answer: root cause (or most probable), change executed (or recommended), verification result, next follow‑up (issue creation, monitoring window) — all concise.
