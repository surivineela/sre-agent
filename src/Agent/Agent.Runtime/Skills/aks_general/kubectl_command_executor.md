# Kubectl Command Execution

Supplementary command execution reference within the AKS operations skill. Provides safe patterns for kubectl reads and controlled (non‑destructive) writes. Do not expose tool names to the user; internally follow these rules.

## Scope

- Read: get, describe, logs, events
- Write (no deletes): create, apply, patch, label, annotate, scale, set image, rollout status/pause/resume, cordon/uncordon/drain (safe flags), taint
- Always: explicit namespace, read-before-write, verify-after-write

## Preconditions

Ensure: cluster context set, namespace known (or discovered), resource kind + name confirmed (no guesses), action classification (read vs write) decided.

## Workflow (Condensed)

1. Plan: goal + current vs desired (≤2 lines)
2. Read: minimal commands to confirm current state
3. Propose (writes only): change + impact + rollback (1–3 lines) → request confirmation
4. Execute single write
5. Verify: expected vs observed (short delta)
6. Decide: more changes or summarize

## Permission / Error Handling

Summarize errors (type, missing permission, needed info). Do NOT dump raw multi-line error output. Ask for user confirmation before retry.

## Write Safety

| Action Type | Required Before | Rollback Example |
|-------------|-----------------|------------------|
| Scale | Current replicas + readiness | Restore previous replica count |
| Patch | Current field values | Reapply prior manifest / inverse patch |
| Probe change | Current probe spec + failure mode | Revert to captured spec |
| Label/Annotate | Current labels/annotations | Remove or restore old value |

Only one write per verification cycle. Use `--dry-run=client -o yaml` when previewing create/apply.

## Command Patterns (Internal Reference)

Reads:

```bash
kubectl get <type> <name?> -n <ns>
kubectl describe <type> <name> -n <ns>
kubectl logs <pod> -n <ns> [--container <c>] [--previous]
kubectl get events -n <ns> --sort-by='.lastTimestamp'
```

Writes (examples):

```bash
kubectl apply -f file.yaml -n <ns>
kubectl scale deployment/<name> --replicas=<n> -n <ns>
kubectl patch <type>/<name> -n <ns> -p '<json-patch>'
kubectl label <type>/<name> key=value -n <ns> --overwrite
kubectl set image deployment/<name> <container>=image:tag -n <ns>
kubectl taint nodes <node> key=value:Effect --overwrite
```

Never: delete resources via this workflow (inform user it's outside permitted scope).

## Verification Template

```text
Observed: <field=value>
Expected: <target>
Result: <match | mismatch + follow-up>
```

Only include fields that changed or validate success (e.g., replicas, ready counts, probe status).

## Examples (Abbreviated)

Scale deployment: Read replicas → propose increase (impact + rollback) → execute scale → verify desired==ready → summarize.
Add label: Read current labels → propose addition + rollback (remove) → label → verify label present.
Investigate restarts: List pods + describe failing pod + fetch previous logs → report restart cause; no write if not required.

## Output Guidance

User-facing answer: outcome + key supporting numbers only (no raw command list, no tool names). Internal reasoning stays hidden unless requested.
