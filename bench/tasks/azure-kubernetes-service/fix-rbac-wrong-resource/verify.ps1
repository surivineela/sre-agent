# PowerShell verify script - converted from shell script
$NAMESPACE = "simple-rbac-setup"
$SERVICE_ACCOUNT = "pod-reader"
$SERVICE_ACCOUNT_USER = "system:serviceaccount:${NAMESPACE}:${SERVICE_ACCOUNT}"

# Check for allowed permissions
try {
    kubectl auth can-i list pods --as=$SERVICE_ACCOUNT_USER -n $NAMESPACE 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ServiceAccount still can't list pods."
        exit 1
    }
}
catch {
    Write-Host "ServiceAccount still can't list pods."
    exit 1
}

# Check for denied permissions
try {
    kubectl auth can-i list pods --as=$SERVICE_ACCOUNT_USER -A 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "ServiceAccount has excessive permissions (can 'list' pods in other namespaces)."
        exit 1
    }
}
catch {
    # Expected behavior - command should fail
}

Write-Host "Verification successful: RBAC role correctly reconfigured."
exit 0