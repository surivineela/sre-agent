$ErrorActionPreference = "Stop"

# Delete namespace if it exists
kubectl delete namespace webshop-frontend --ignore-not-found

# Create a fresh namespace
kubectl create namespace webshop-frontend

# Apply the service and deployment with the invalid node selector
kubectl apply -f artifacts/service.yaml
kubectl apply -f artifacts/deployment.yaml

# Wait for the deployment to be available or timeout after 30 seconds
Write-Host "Waiting for resources to be created..."
kubectl wait --for=condition=Available=False --timeout=30s deployment/web-app-deployment -n webshop-frontend

# Check the service has no endpoints (due to deployment with invalid node selector)
$endpoints = kubectl get endpoints web-app-service -n webshop-frontend -o jsonpath='{.subsets}'
if ([string]::IsNullOrEmpty($endpoints)) {
    Write-Host "Setup successful: Service has no endpoints as expected"
} else {
    Write-Host "Unexpected state: Service has endpoints"
    exit 1
}
