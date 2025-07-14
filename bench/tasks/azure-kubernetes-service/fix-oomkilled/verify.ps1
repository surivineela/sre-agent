$NAMESPACE = "webapp-backend"
$DEPLOYMENT = "backend-api"

# Check if the deployment is ready
kubectl wait --for=condition=Available deployment/$DEPLOYMENT -n $NAMESPACE --timeout=60s
if ($LASTEXITCODE -ne 0) {
    Write-Host "Deployment is not available"
    exit 1
}

# Check if pods are running
kubectl wait --for=condition=Ready pod -l app=backend-api -n $NAMESPACE --timeout=30s
if ($LASTEXITCODE -ne 0) {
    Write-Host "Pods are not ready"
    exit 1
}

# Check that there are no recent OOMKilled events
$oomKilledCount = kubectl get events -n $NAMESPACE --field-selector reason=OOMKilling --sort-by='.lastTimestamp' -o json | ConvertFrom-Json | Select-Object -ExpandProperty items | Measure-Object | Select-Object -ExpandProperty Count

if ($oomKilledCount -gt 0) {
    # Check if the most recent OOMKilled event is from the last 2 minutes (indicating ongoing issues)
    $recentOOMKilled = kubectl get events -n $NAMESPACE --field-selector reason=OOMKilling --sort-by='.lastTimestamp' -o jsonpath='{.items[-1].lastTimestamp}' 2>$null
    if ($recentOOMKilled) {
        try {
            $recentTime = [DateTime]::Parse($recentOOMKilled)
            $currentTime = Get-Date
            if (($currentTime - $recentTime).TotalSeconds -lt 120) {
                Write-Host "Recent OOMKilled events detected"
                exit 1
            }
        } catch {
            # If we can't parse the time, assume it's recent
            Write-Host "Recent OOMKilled events detected"
            exit 1
        }
    }
}

Write-Host "Pod is running successfully without OOMKilled events"
exit 0
