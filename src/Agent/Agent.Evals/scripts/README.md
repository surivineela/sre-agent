# Test Scripts

This folder contains scripts to run the `GeneralAgentTests_DetailedComparison` test efficiently.

## Usage

**Important**: Run these scripts from the root directory: `/Users/sunjianbo/work/ai/sreagent-runtime/`

### Bash Script (macOS/Linux)
```bash
./src/Agent/Agent.Evals/scripts/run_test_10x.sh./src/Agent/Agent.Evals/scripts/run_test_10x.sh
```

### PowerShell Script (Cross-platform)
```bash
pwsh ./src/Agent/Agent.Evals/scripts/run_test_10x.ps1
```

## What the scripts do:
1. Build the test project once in Release mode
2. Run the `GeneralAgentTests_DetailedComparison` test 10 times
3. Track and report pass/fail statistics
4. Use optimized flags to avoid rebuilding and reduce output noise

## Requirements:
- .NET SDK installed
- Run from the repository root directory
- For PowerShell script: PowerShell Core (pwsh) installed
