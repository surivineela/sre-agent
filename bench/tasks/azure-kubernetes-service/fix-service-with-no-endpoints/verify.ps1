# Set error action preference to stop on errors (equivalent to set -e)
$ErrorActionPreference = "Stop"

# Check if the deployment exists
Write-Host "Checking if deployment exists..."
try {
    kubectl get deployment web-app-deployment -n webshop-frontend 2>$null | Out-Null
}
catch {
    Write-Host "Deployment 'web-app-deployment' does not exist in namespace 'webshop-frontend'"
    exit 1
}

# Check if pods are being created successfully
Write-Host "Waiting for pods to become ready..."
try {
    kubectl wait --for=condition=Ready pods -l app=web-app -n webshop-frontend --timeout=60s
    if ($LASTEXITCODE -ne 0) {
        throw "kubectl wait failed"
    }
}
catch {
    Write-Host "Pods are not reaching Ready state after fixing the node selector"
    exit 1
}

# Verify that the service now has endpoints
Write-Host "Checking service endpoints..."
try {
    $ENDPOINTS = kubectl get endpoints web-app-service -n webshop-frontend -o jsonpath='{.subsets[0].addresses}' 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to get endpoints"
    }
    
    if ([string]::IsNullOrWhiteSpace($ENDPOINTS)) {
        Write-Host "Service still has no endpoints after fixing the deployment"
        exit 1
    }
}
catch {
    Write-Host "Service still has no endpoints after fixing the deployment"
    exit 1
}

Write-Host "All checks passed successfully!"
