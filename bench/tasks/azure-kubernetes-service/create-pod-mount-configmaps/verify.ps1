$NAMESPACE = "color-size-settings"

# Check if namespace exists
try {
    kubectl get namespace $NAMESPACE 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Namespace '$NAMESPACE' does not exist"
        exit 1
    }
} catch {
    Write-Host "Namespace '$NAMESPACE' does not exist"
    exit 1
}

# Check if configmaps exist
try {
    kubectl get configmap color-settings -n $NAMESPACE 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ConfigMap 'color-settings' does not exist in namespace '$NAMESPACE'"
        exit 1
    }
} catch {
    Write-Host "ConfigMap 'color-settings' does not exist in namespace '$NAMESPACE'"
    exit 1
}

try {
    kubectl get configmap size-settings -n $NAMESPACE 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ConfigMap 'size-settings' does not exist in namespace '$NAMESPACE'"
        exit 1
    }
} catch {
    Write-Host "ConfigMap 'size-settings' does not exist in namespace '$NAMESPACE'"
    exit 1
}

# Check configmap contents
$colorValue = kubectl get configmap color-settings -n $NAMESPACE -o jsonpath='{.data.color}'
if ($colorValue -ne "blue") {
    Write-Host "ConfigMap 'color-settings' has incorrect value for key 'color': '$colorValue', expected: 'blue'"
    exit 1
}

$sizeValue = kubectl get configmap size-settings -n $NAMESPACE -o jsonpath='{.data.size}'
if ($sizeValue -ne "medium") {
    Write-Host "ConfigMap 'size-settings' has incorrect value for key 'size': '$sizeValue', expected: 'medium'"
    exit 1
}

# Wait for pod to be ready
kubectl wait --for=condition=Ready pod/pod1 -n $NAMESPACE --timeout=60s
if ($LASTEXITCODE -ne 0) {
    Write-Host "Pod 'pod1' is not ready in namespace '$NAMESPACE'"
    exit 1
}

# Verify pod has the correct image
$podImage = kubectl get pod pod1 -n $NAMESPACE -o jsonpath='{.spec.containers[0].image}'
if ($podImage -ne "nginx:alpine") {
    Write-Host "Pod has incorrect image: $podImage, expected: nginx:alpine"
    exit 1
}

# Verify the values are accessible in the pod
Write-Host "Verifying environment variable in pod..."
$envTest = kubectl exec pod1 -n $NAMESPACE -- sh -c 'echo $COLOR'
if ($envTest -ne "blue") {
    Write-Host "Environment variable 'COLOR' is not accessible in the pod or has incorrect value: '$envTest'"
    exit 1
}

Write-Host "Verifying volume mount in pod..."
$volumeTest = kubectl exec pod1 -n $NAMESPACE -- cat /etc/sizes/size
if ($volumeTest -ne "medium") {
    Write-Host "Volume mount is not accessible in the pod or file has incorrect content: '$volumeTest'"
    exit 1
}

# All verifications passed
Write-Host "All verifications passed!"
exit 0
