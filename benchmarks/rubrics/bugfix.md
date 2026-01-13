# Bugfix Rubric

Human evaluation for bugfix tasks. Score each metric 1-5.

---

## Scoring Scale

| Score | Meaning |
|-------|--------|
| 5 | Excellent — exceeds expectations, no issues |
| 4 | Good — meets expectations, minor issues only |
| 3 | Acceptable — works but has notable gaps |
| 2 | Poor — partially works, significant issues |
| 1 | Failing — does not work or missing entirely |

---

## Metrics

| Metric | Score (1-5) | Notes |
|--------|-------------|-------|
| **Root Cause Analysis** — Correctly identified and addressed root cause, not just symptoms | | |
| **Fix Quality** — Bug no longer reproduces, edge cases handled, no new bugs | | |
| **Regression Test** — Test added that would have caught the original bug | | |
| **Minimality** — Changes targeted and minimal, no unrelated modifications | | |
| **Repo Alignment** — Follows existing patterns and conventions | | |

---

## Common Failure Modes

| Failure | What to Look For |
|---------|------------------|
| **Symptom fix** | Bug appears fixed but root cause not addressed; may recur |
| **Shotgun fix** | Multiple unrelated changes hoping one fixes it |
| **Missing regression test** | Bug fixed but no test to prevent recurrence |
| **Over-scoped fix** | Includes refactoring or improvements beyond the bug |
| **Wrong root cause** | Fix works by accident; different inputs may still fail |
| **New bugs introduced** | Fix breaks other functionality |
