---
name: diagnostic_memory
description: |
  Diagnose memory issues (leaks, high heap, OOM) for Azure workloads by analyzing dumps: surface top types, GC root chains, sizes, counts, visualization, and remediation guidance.
tools:
  - GetMemoryAnalysis
  - PlotBarChart
---

# Diagnostic Memory Skill

## Purpose

Diagnose memory-related issues (high RSS/heap, OutOfMemory exceptions, suspicious GC pauses) for Azure compute workloads (Container Apps, Web/Function Apps, AKS pods) using memory dump analysis. Identify largest memory consumers and GC root chains and surface clear remediation insights.

## When to Use

Invoke this skill when:

- Memory usage or leak suspected (sustained growth, OOM, degraded performance).
- Need to understand what types / root chains dominate heap.
- Prior to proposing remediation for caching, buffering, or allocation hotspots.

## Inputs Required

- resource_id (or equivalent handle to target workload)
- (Optional) time window or dump identifier if multiple dumps exist

Use only minimal necessary parameters.

## Objectives

1. List top memory-consuming types and GC root chains.
2. Quantify size (bytes + human-readable) and object counts.
3. Visualize relative contribution (bar chart preferred).
4. Interpret patterns indicating leaks or inefficiencies.
5. Recommend focused remediation options.

## Tools

- GetMemoryAnalysis – obtain top GC root chains + type sizes.
- PlotBarChart – visualize per-root size (bytes). Fallback: heatmap; final fallback: textual list.

## Workflow

1. Brief pre-action checklist (3–7 bullets) tailored to the current case.
2. Call GetMemoryAnalysis (state purpose + inputs in one line).
3. Validate result: at least five root chains; each has path, size, count. If inconsistent, retry once; on second failure, proceed with best-effort textual summary.
4. Build visualization with PlotBarChart (sizes in bytes). If it fails: heatmap; if that fails: textual ordered list.
5. Produce outputs in this order:
   a. Visualization (or fallback) of root chain sizes.
   b. Table of top five root chains (path, size bytes + human-readable, count).
   c. Interpretive summary: dominant types, notable chains, leak indicators, remediation suggestions.
6. Add a one-line validation note after each output confirming sanity (e.g., count of chains, largest size plausible).
7. Highlight any retry and difference between attempts if a retry occurred.

## Reporting Guidelines

- Preserve full chain paths (do not truncate) unless excessively long; if truncation needed, indicate clearly with ellipsis.
- Sort by descending size.
- Show both raw bytes and human-readable (e.g., 183,500,800 B (175 MB)).
- Keep table concise (five entries unless fewer available).
- Avoid unnecessary multi-message fragmentation beyond the required three output blocks.

## Interpretation Patterns

- Large System.Byte[] / System.String[]: buffering, serialization, or log/message accumulation.
- Large collections (Dictionary, ConcurrentQueue, List) rooted by singletons/static: retention leak or missing eviction.
- Growing HttpClient-related headers / strings: misuse of HttpClient instances or per-request creation.
- Large arrays of POCOs: unbounded caching or batching logic.
- ThreadPool / work queues accumulation: producer faster than consumer.

## Remediation Examples

- Introduce eviction (LRU / TTL) for caches.
- Stream large payloads; avoid full buffering.
- Reuse a single HttpClient instance; clear custom headers per request.
- Break large batches into smaller chunks.
- Investigate consumer lag / apply backpressure for queues.

## Failure & Retry

- On tool failure: state concise reason, retry once.
- After second failure: supply partial data (if any), recommend collecting a fresh dump, verifying permissions, or narrowing resource scope.

## Quality & Validation Checklist (apply lightly)

- Five root chains present (or all available if <5).
- Largest root size not absurd (e.g., > total heap reported).
- Sum of shown sizes aligns directionally with overall heap usage.
- Visualization labels readable.

## Example (Summary Snippet)

The heap is dominated by System.Byte[] (~1.75 GB). Largest chain:
System.Object[] → ContosoChat2.Customers+Processor → ... → System.Byte[] (~75 MB segment)
Pattern suggests customer cache retention; recommend adding eviction + size monitoring.
Validation: Chain count=5, largest type plausible, pattern indicates cache.

Keep outputs concise, actionable, and directly tied to observed data.
