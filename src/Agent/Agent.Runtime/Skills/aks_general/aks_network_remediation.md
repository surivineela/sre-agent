# AKS Network Remediation

Supplementary networking reference within the single AKS operations skill. Covers DNS, Service/Endpoint resolution, east–west pod connectivity, ingress, egress, NetworkPolicy, CNI/IPAM, and Azure network dependencies—within safe, auditable read/update scope (no destructive deletes).

## Fast Symptom Index

| Symptom | Jump To | Primary Signals |
|---------|---------|-----------------|
| Name resolution failures | DNS | CoreDNS pod health, ConfigMap, /etc/resolv.conf |
| Service has no endpoints / 503s | Service / ClusterIP | Endpoints object, selector mismatch |
| Pod-to-pod unreachable | East–West Connectivity | NetworkPolicy, CNI logs, node routing |
| Ingress EXTERNAL-IP pending / 4xx / 5xx | Ingress | Events, controller logs, subnet IP usage |
| Outbound timeouts / TLS failures | Egress | NAT Gateway SNAT ports, Firewall/NSG rules, UDR |
| Connections fail only with policies enabled | NetworkPolicy | Policy YAML, default-deny presence |
| Pod scheduling hangs / IP exhaustion | CNI/IPAM | CNI logs, subnet free IPs, maxPods alignment |
| LB traffic blocked / asymmetric routing | Azure NSG/UDR | Effective NSG, route tables |
| Sidecar / mTLS policy blocks | Service Mesh | Sidecar injection, mesh policies |

## Core Pattern

1. Define Flow: source (namespace/pod) → destination (service/pod/host:port/protocol) + direction (east–west | ingress | egress).
2. Classify using the index above.
3. Pull minimal evidence (category specific sections below).
4. Identify single blocking layer (DNS → Service → Endpoint → Pod → Policy/CNI/Azure network).
5. Propose minimal fix (allow rule, selector correction, rollback bad policy, capacity adjustment) + rollback.
6. Verify original failing flow from original source context.
7. Record change (`change_propagation.md`) and summarize.

## Permission / Access Errors

Summarize needed role or missing permission only—no raw error dump. After user grants, retry limited scope evidence.

## Category Playbooks (Condensed)

### DNS

Evidence: CoreDNS pod readiness, CoreDNS logs (tail), CoreDNS ConfigMap (forwarders/stubDomains), pod `/etc/resolv.conf` (search + ndots).
Fixes: correct forwarders, remove invalid stubDomains, ensure UDP/TCP 53 allowed, align NodeLocal DNS config.
Verify: `dig service.namespace.svc.cluster.local +short` and external domain success.

### Service / ClusterIP

Evidence: Service describe (selector), Endpoints object, pod labels, kube-proxy logs (if systemic).
Fixes: align labels vs selector, correct port/targetPort mismatches, restart pod if not listening.
Verify: curl from test pod to service DNS name and port.

### East–West Pod Connectivity

Evidence: Pod IPs + node placement, NetworkPolicies (namespace + global), CNI / Cilium logs, presence of default-deny policies.
Fixes: permissive allow for required ports/namespaces → refine; resolve CNI datapath errors; address IPAM exhaustion.
Verify: `nc -vz DEST_POD_IP PORT` or curl from source pod.

### Ingress

Evidence: Service (type LoadBalancer) / Ingress resource events, controller logs, EXTERNAL-IP status, subnet free IPs.
Fixes: fix host/path rules, correct backend port, free or assign IP, adjust annotations/class.
Verify: `curl -v http(s)://EXTERNAL_ADDRESS` + controller logs show healthy upstream.

### Egress

Evidence: Test pod curl, NAT Gateway SNAT metrics (if available), route table entries, Firewall / NSG rules, DNS for private endpoints.
Fixes: allow rule additions (least privilege), add NAT public IPs for SNAT exhaustion, correct private DNS zone, fix UDR asymmetry.
Verify: repeated curl success (no intermittent timeouts).

### NetworkPolicy

Evidence: All policies in source & destination namespaces; identify default-deny; confirm required selectors/ports.
Fixes: add targeted allow (DNS egress, required app ports), then narrow.
Verify: flow succeeds while rule scope remains least privilege.

### CNI / IPAM

Evidence: Pod events referencing CNI, CNI daemon logs, subnet IP utilization, maxPods vs node allocation.
Fixes: expand subnet / add secondary range, adjust maxPods, restart faulty CNI pods.
Verify: new pod schedules with IP; prior CNI errors stop.

### Azure NSG / UDR

Evidence: Effective NSG for NIC/subnet, NSG rule evaluation, route table (UDR) entries for path symmetry.
Fixes: add explicit allow (LB probe, required ports), correct asymmetric route, ensure return path.
Verify: sustained successful traffic.

### Service Mesh

Evidence: Sidecar injection status, mTLS / authorization policies, ports excluded/included.
Fixes: align mTLS modes, adjust policies, exclude health probes if needed.
Verify: mesh telemetry shows healthy traffic; original request succeeds.

## Evidence Summary Table (Optional Output)

| Layer | Key Finding | Impact | Action |
|-------|-------------|--------|--------|
| DNS | StubDomain forwarding 10.0.0.5 unreachable | Name resolution fails | Remove entry / correct IP |

## Verification Checklist

- Re-test original failing flow (same source pod or recreated equivalent)
- Confirm no new warnings in relevant logs (CoreDNS / controller / CNI)
- Ensure fix is scoped minimally (policy rules, ports, CIDRs)
- Record change (`change_propagation.md`) if any config adjusted

## Reporting Format

Answer sentence: concise resolution or remaining blocker.
Supporting bullets: flow definition, root cause, action taken, verification result, follow-up (if any).
No raw command dump—summarize evidence.

## Examples (Abbreviated)

1. Default-deny blocked DNS → added kube-dns egress allow (UDP/TCP 53) → name resolution restored.
2. Ingress EXTERNAL-IP pending due to subnet exhaustion → assigned available static IP → ingress reachable (200 OK).
3. SNAT exhaustion → added second public IP to NAT Gateway → peak egress success 100%.
4. Service selector mismatch → updated selector label → endpoints populated; 503 resolved.
