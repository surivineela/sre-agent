# Check if namespace exists
try {
    kubectl get namespace limits-test 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Namespace 'limits-test' does not exist"
        exit 1
    }
} catch {
    Write-Host "Namespace 'limits-test' does not exist"
    exit 1
}

# Wait for pod to be ready
kubectl wait --for=condition=Ready pod/resource-limits-pod -n limits-test --timeout=60s
if ($LASTEXITCODE -ne 0) {
    Write-Host "Pod 'resource-limits-pod' is not ready in namespace 'limits-test'"
    exit 1
}

# Verify the pod has the correct image
$podImage = kubectl get pod resource-limits-pod -n limits-test -o jsonpath='{.spec.containers[0].image}'
if ($podImage -ne "httpd:alpine") {
    Write-Host "Pod has incorrect image: $podImage, expected: httpd:alpine"
    exit 1
}

# Verify the container name
$containerName = kubectl get pod resource-limits-pod -n limits-test -o jsonpath='{.spec.containers[0].name}'
if ($containerName -ne "my-container") {
    Write-Host "Container has incorrect name: $containerName, expected: my-container"
    exit 1
}

# Verify CPU request
$cpuRequest = kubectl get pod resource-limits-pod -n limits-test -o jsonpath='{.spec.containers[0].resources.requests.cpu}'
if ($cpuRequest -ne "60m") {
    Write-Host "Container has incorrect CPU request: $cpuRequest, expected: 60m"
    exit 1
}

# Verify CPU limit
$cpuLimit = kubectl get pod resource-limits-pod -n limits-test -o jsonpath='{.spec.containers[0].resources.limits.cpu}'
if ($cpuLimit -ne "600m") {
    Write-Host "Container has incorrect CPU limit: $cpuLimit, expected: 600m"
    exit 1
}

# Verify memory request
$memoryRequest = kubectl get pod resource-limits-pod -n limits-test -o jsonpath='{.spec.containers[0].resources.requests.memory}'
if ($memoryRequest -ne "62Mi") {
    Write-Host "Container has incorrect memory request: $memoryRequest, expected: 62Mi"
    exit 1
}

# Verify memory limit
$memoryLimit = kubectl get pod resource-limits-pod -n limits-test -o jsonpath='{.spec.containers[0].resources.limits.memory}'
if ($memoryLimit -ne "62Mi") {
    Write-Host "Container has incorrect memory limit: $memoryLimit, expected: 62Mi"
    exit 1
}

# All verifications passed
Write-Host "All verifications passed!"
exit 0
