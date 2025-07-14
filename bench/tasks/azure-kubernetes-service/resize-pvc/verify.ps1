# Configuration
$PVC_NAME = "storage-pvc"
$EXPECTED_SIZE = "15Gi"

Write-Host "Attempting to get PV name from PVC: $PVC_NAME"

# Dynamically get the PV name from the PVC
$PV_NAME = kubectl get pvc $PVC_NAME -n resize-pv -o jsonpath='{.spec.volumeName}'

if ([string]::IsNullOrEmpty($PV_NAME)) {
    Write-Host "Error: Could not retrieve PersistentVolume name for PVC '$PVC_NAME'. Make sure the PVC exists and is bound." -ForegroundColor Red
    exit 1
}

# Check if the PV reaches the expected capacity
$waitResult = kubectl wait --for="jsonpath={.spec.capacity.storage}=$EXPECTED_SIZE" pv/$PV_NAME --timeout=30s
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAILURE: PersistentVolume '$PV_NAME' did not reach the expected capacity of $EXPECTED_SIZE." -ForegroundColor Red
    exit 1
} else {
    Write-Host "SUCCESS: PersistentVolume '$PV_NAME' reached the expected capacity of $EXPECTED_SIZE." -ForegroundColor Green
}