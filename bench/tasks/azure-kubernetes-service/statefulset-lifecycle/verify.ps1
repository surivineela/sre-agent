# Verify only db-0 and db-1 remain
$pods = @("db-0", "db-1")

foreach ($pod in $pods) {
    # Check if pod exists
    $podCheck = kubectl get pod $pod -n statefulset-test 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Pod $pod not found"
        exit 1
    }
    
    # Get data from pod
    $data = kubectl exec $pod -n statefulset-test -- cat /data/test 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to get data from $pod"
        exit 1
    }
    
    if ($data -notlike "*test*") {
        Write-Error "Data missing or incorrect in $pod"
        exit 1
    }
}

# Wait for scale-down: 2 ready pods and deletion of old pods
Write-Host "Waiting for pods db-2, db-3, db-4 to be deleted..."
kubectl wait pod/db-2 pod/db-3 pod/db-4 -n statefulset-test --for=delete --timeout=120s

if ($LASTEXITCODE -ne 0) {
    Write-Error "Timeout waiting for pods to be deleted"
    exit 1
}

Write-Host "Verification completed successfully"
exit 0
