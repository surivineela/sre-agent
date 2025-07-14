kubectl delete namespace webapp-backend --ignore-not-found

# Create namespace
kubectl create namespace webapp-backend

# Apply the deployment from artifacts
kubectl apply -f artifacts/memory-hungry-app.yaml

# Wait for the deployment to be created
kubectl rollout status deployment/backend-api -n webapp-backend --timeout=60s

# Wait until an OOMKilled event is detected (timeout after 30s)
Write-Host "Waiting for OOMKilled event to occur..."
for ($i = 1; $i -le 15; $i++) {
    $oomKilledCount = kubectl get events -n webapp-backend --field-selector reason=OOMKilling -o json | ConvertFrom-Json | Select-Object -ExpandProperty items | Measure-Object | Select-Object -ExpandProperty Count
    if ($oomKilledCount -gt 0) {
        Write-Host "OOMKilled event detected."
        break
    }
    Start-Sleep 2
}
