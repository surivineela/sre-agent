# PowerShell script to run GeneralAgentTests_DetailedComparison 10 times efficiently
# Usage: From the root directory (/Users/sunjianbo/work/ai/sreagent-runtime/), run:
# pwsh ./src/Agent/Agent.Evals/scripts/run_test_10x.ps1

Write-Host "🔧 Building project once in Release mode..." -ForegroundColor Cyan
dotnet build src/Agent/Agent.Evals/Agent.Evals.csproj --verbosity quiet --no-restore

Write-Host "🧪 Running GeneralAgentTests_DetailedComparison 10 times..." -ForegroundColor Cyan
Write-Host "======================================================="

$passed = 0
$failed = 0

for ($i = 1; $i -le 10; $i++) {
    Write-Host "Run $i/10:" -NoNewline
    
    $result = dotnet test src/Agent/Agent.Evals/Agent.Evals.csproj `
        --filter "TestMethod=GeneralAgentTests_DetailedComparison" `
        --no-build `
        --no-restore `
        --verbosity quiet `
        --logger "console;verbosity=minimal" 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✅ PASSED" -ForegroundColor Green
        $passed++
    } else {
        Write-Host "  ❌ FAILED" -ForegroundColor Red
        $failed++
    }
}

Write-Host "======================================================="
Write-Host "📊 Results Summary:" -ForegroundColor Cyan
Write-Host "   ✅ Passed: $passed/10" -ForegroundColor Green
Write-Host "   ❌ Failed: $failed/10" -ForegroundColor Red
Write-Host "   📈 Success Rate: $($passed * 100 / 10)%" -ForegroundColor Yellow

if ($failed -eq 0) {
    Write-Host "🎉 All tests passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "⚠️  Some tests failed. Check individual runs for details." -ForegroundColor Yellow
    exit 1
}
