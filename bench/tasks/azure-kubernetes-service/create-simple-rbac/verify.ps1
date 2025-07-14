$NAMESPACE = "create-simple-rbac"
$SERVICE_ACCOUNT = "reader-sa"
$SERVICE_ACCOUNT_USER = "system:serviceaccount:${NAMESPACE}:${SERVICE_ACCOUNT}"

# Check for allowed permissions
kubectl auth can-i get pods --as=$SERVICE_ACCOUNT_USER -n $NAMESPACE 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "ServiceAccount cannot 'get' pods."
    exit 1
}

kubectl auth can-i list pods --as=$SERVICE_ACCOUNT_USER -n $NAMESPACE 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "ServiceAccount cannot 'list' pods."
    exit 1
}

# Check for denied permissions
kubectl auth can-i delete pods --as=$SERVICE_ACCOUNT_USER -n $NAMESPACE 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "ServiceAccount has excessive permissions (can 'delete' pods)."
    exit 1
}

kubectl auth can-i create pods --as=$SERVICE_ACCOUNT_USER -n $NAMESPACE 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "ServiceAccount has excessive permissions (can 'create' pods)."
    exit 1
}

kubectl auth can-i create pods --as=$SERVICE_ACCOUNT_USER 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "ServiceAccount has excessive permissions (can 'create' pods in other namespace)."
    exit 1
}

kubectl auth can-i list pods --as=$SERVICE_ACCOUNT_USER -A 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "ServiceAccount has excessive permissions (can 'list' pods in other namespace)."
    exit 1
}

Write-Host "Verification successful: RBAC role and binding correctly configured."
exit 0
