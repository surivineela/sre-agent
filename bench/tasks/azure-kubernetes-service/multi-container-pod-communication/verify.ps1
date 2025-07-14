#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"

# Wait for pod to be running
Write-Host "Waiting for communication-pod to be ready..."
$waitResult = kubectl wait --for=condition=Ready pod/communication-pod -n multi-container-test --timeout=60s
if ($LASTEXITCODE -ne 0) {
    Write-Host "Pod failed to reach Ready state in time"
    Write-Host "Current pod status:"
    kubectl describe pod communication-pod -n multi-container-test
    exit 1
}

Write-Host "Pod is ready. Verifying configuration..."

# Then verify that both containers are running
$containers = kubectl get pod communication-pod -n multi-container-test -o jsonpath='{.spec.containers[*].name}'
if (-not ($containers -like "*web*") -or -not ($containers -like "*logger*")) {
    Write-Host "Pod does not have both 'web' and 'logger' containers"
    exit 1
}

# Does the shared volume exist
$volumes = kubectl get pod communication-pod -n multi-container-test -o jsonpath='{.spec.volumes[*].name}'
if (-not ($volumes -like "*logs-volume*")) {
    Write-Host "Pod does not have the required 'logs-volume' volume"
    exit 1
}

# Is web server accessible
Write-Host "Testing web server access..."
$httpCode = kubectl exec communication-pod -n multi-container-test -c web-server -- curl -s -o /dev/null -w "%{http_code}" localhost:80
if ($httpCode -ne "200") {
    Write-Host "Web server is not accessible (HTTP code: $httpCode)"
    exit 1
}

# Logger container can see the access logs
Write-Host "Verifying logger container can access nginx logs..."
$logCheckResult = kubectl exec communication-pod -n multi-container-test -c logger -- ls -la /var/log/nginx/access.log
if ($LASTEXITCODE -ne 0) {
    Write-Host "Logger container cannot access nginx logs"
    exit 1
}

Write-Host "All verification checks passed!"
exit 0
