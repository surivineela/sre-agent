# Refactor Rubric

Human evaluation for refactor tasks. Score each metric 1-5.

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
| **Goal Achievement** — Refactor objective met, desired state reached | | |
| **Behavior Preservation** — No functional changes, all existing tests pass unchanged | | |
| **Scope Discipline** — Stayed within boundaries, no "while I'm here" changes | | |
| **Code Quality** — Improved structure, readable, follows repo patterns | | |

---

## Common Failure Modes

| Failure | What to Look For |
|---------|------------------|
| **Behavior change** | Tests fail or pass differently; subtle logic changes |
| **Scope creep** | "While I'm here" changes beyond the stated refactor goal |
| **Modified test assertions** | Changed expected values instead of preserving behavior |
| **Incomplete refactor** | Goal partially achieved; inconsistent state left behind |
| **Over-abstraction** | Introduced patterns or layers not justified by the goal |
| **Broken callers** | Refactored code works but callers/consumers are broken |
