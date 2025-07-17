#!/bin/bash

# Script to run GeneralAgentTests_DetailedComparison 10 times efficiently
# Usage: From the root directory (/Users/sunjianbo/work/ai/sreagent-runtime/), run:
# ./src/Agent/Agent.Evals/scripts/run_test_10x.sh

set -e

echo "🔧 Building project once in Release mode..."
dotnet build src/Agent/Agent.Evals/Agent.Evals.csproj --verbosity quiet --no-restore

echo "🧪 Running GeneralAgentTests_DetailedComparison 10 times..."
echo "======================================================="

PASSED=0
FAILED=0

for i in {1..10}; do
    echo "Run $i/10:"

    if dotnet test src/Agent/Agent.Evals/Agent.Evals.csproj \
        --filter "TestMethod=GeneralAgentTests_DetailedComparison" \
        --no-build \
        --no-restore \
        --verbosity quiet \
        --logger "console;verbosity=minimal" >/dev/null 2>&1; then
        echo "  ✅ PASSED"
        ((PASSED++))
    else
        echo "  ❌ FAILED"
        ((FAILED++))
    fi
done

echo "======================================================="
echo "📊 Results Summary:"
echo "   ✅ Passed: $PASSED/10"
echo "   ❌ Failed: $FAILED/10"
echo "   📈 Success Rate: $((PASSED * 100 / 10))%"

if [ $FAILED -eq 0 ]; then
    echo "🎉 All tests passed!"
    exit 0
else
    echo "⚠️  Some tests failed. Check individual runs for details."
    exit 1
fi
