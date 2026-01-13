# Manual Benchmark Kit

Compare agentic dev systems (Copilot, Claude, etc.) on repeatable tasks using standardized prompts and human evaluation.

## Purpose

This kit provides:
- **Standardized task definitions** with copy/paste prompts
- **Consistent run conditions** for fair comparison
- **Human-evaluated rubrics** (no automation)

## Standard Conditions Checklist

Before each run, verify:

- [ ] Same baseline commit SHA across all agents being compared
- [ ] Clean working tree (`git status` shows no changes)
- [ ] Same time budget (default: **45 minutes**)
- [ ] Same allowed actions:
  - Agent may run tests, linters, build commands
  - Human does **not** patch code manually
  - Human may clarify requirements if agent asks
- [ ] Stop condition: time budget hit **OR** acceptance criteria met and tests green

## Run Procedure

### 1. Prepare

```bash
# Checkout the baseline SHA
git checkout <BASELINE_SHA>

# Create a branch for this run
git checkout -b bench/<task-id>/<agent>/run-01
# Example: bench/T001/copilot/run-01
```

### 2. Execute

1. Open the task file from `/benchmarks/tasks/Txxx-*.md`
2. Paste prompts **in order**:
   - **Planning Prompt** → let agent analyze
   - **Implementation Prompt** → let agent code
   - **Debug Prompt** → only if tests fail
   - **Wrap-up Prompt** → get PR summary
3. Run verification commands as specified in the task
4. Stop when time budget expires or acceptance criteria are met

### 3. Save Artifacts

Create folder: `/benchmarks/runs/<YYYY-MM-DD>/<TASK-ID>/<AGENT>/`

Save:
| File | Contents |
|------|----------|
| `transcript.md` | Copy/paste full conversation |
| `patch.diff` | `git diff > patch.diff` |
| `notes.md` | Commands run, results, timing, issues |
| `screenshots/` | Optional supporting evidence |

```bash
# Example
mkdir -p benchmarks/runs/2026-01-08/T001/copilot
git diff > benchmarks/runs/2026-01-08/T001/copilot/patch.diff
```

### 4. Evaluate

Open the appropriate rubric from `/benchmarks/rubrics/`:
- `feature.md` — for feature tasks
- `bugfix.md` — for bugfix tasks
- `refactor.md` — for refactor tasks

Review the agent's output against the checklist. Record findings in `notes.md`.

## Directory Structure

```
/benchmarks/
├── README.md           # This file
├── tasks/              # Task definitions (one .md per task)
│   ├── T001-feature-example.md
│   ├── T002-bugfix-example.md
│   └── T003-refactor-example.md
├── rubrics/            # Human evaluation checklists
│   ├── feature.md
│   ├── bugfix.md
│   └── refactor.md
└── runs/               # Saved run artifacts (gitignored except .gitkeep)
    └── .gitkeep
```

## Quick Reference

| Task Type | Rubric | Typical Focus |
|-----------|--------|---------------|
| Feature | `rubrics/feature.md` | New functionality, edge cases, tests |
| Bugfix | `rubrics/bugfix.md` | Root cause, regression tests, minimal change |
| Refactor | `rubrics/refactor.md` | Behavior preservation, code quality, no scope creep |
