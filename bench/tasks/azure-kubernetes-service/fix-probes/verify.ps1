# Check if the pod is in Running state with Ready status
Write-Host "Checking if the pod is running and ready..."

# Wait up to 30 seconds for pod to become ready using kubectl wait
kubectl wait --for=condition=Ready pod -l app=webapp -n health-check --timeout=30s
if ($LASTEXITCODE -eq 0) {
    Write-Host "Success: Pod is now Ready"
    
    # Check if probes exist at all
    $livenessExists = kubectl get deploy webapp -n health-check -o jsonpath='{.spec.template.spec.containers[0].livenessProbe}'
    $readinessExists = kubectl get deploy webapp -n health-check -o jsonpath='{.spec.template.spec.containers[0].readinessProbe}'
    
    if ([string]::IsNullOrEmpty($livenessExists) -or [string]::IsNullOrEmpty($readinessExists)) {
        Write-Host "Failure: One or both probes have been removed completely."
        Write-Host "Probes should be fixed, not removed."
        exit 1
    }
    
    # Get the current probe configurations
    $livenessPath = kubectl get deploy webapp -n health-check -o jsonpath='{.spec.template.spec.containers[0].livenessProbe.httpGet.path}'
    $readinessPath = kubectl get deploy webapp -n health-check -o jsonpath='{.spec.template.spec.containers[0].readinessProbe.httpGet.path}'
    
    Write-Host "Current liveness probe path: $livenessPath"
    Write-Host "Current readiness probe path: $readinessPath"
    
    # Verify the probes are not using the nonexistent paths and have valid paths set
    if ($livenessPath -ne "/get_status" -and $readinessPath -ne "/is_ready" -and 
        -not [string]::IsNullOrEmpty($livenessPath) -and -not [string]::IsNullOrEmpty($readinessPath)) {
        Write-Host "Success: Both probe paths have been fixed"
        
        # Check if pod is stable with no recent restarts
        $restarts = kubectl get pods -n health-check -l app=webapp -o jsonpath='{.items[0].status.containerStatuses[0].restartCount}'
        if ([int]$restarts -lt 1) {
            Write-Host "Success: Pod is stable with acceptable number of restarts"
            exit 0
        } else {
            Write-Host "Failure: Pod has too many restarts: $restarts"
            exit 1
        }
    } else {
        Write-Host "Failure: One or both probe paths are still incorrect or missing:"
        Write-Host "Liveness path: $livenessPath"
        Write-Host "Readiness path: $readinessPath"
        exit 1
    }
} else {
    Write-Host "Failure: Pod is not Ready after waiting"
    kubectl get pods -n health-check -l app=webapp
    exit 1
}
