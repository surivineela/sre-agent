# Check if service has endpoints
$endpoints = kubectl get endpoints nginx -n web -o jsonpath='{.subsets[0].addresses}'

if (-not [string]::IsNullOrEmpty($endpoints)) {
    # Verify service can access the pod
    $testResult = kubectl run -n web test-connection --image=busybox --restart=Never --rm -i --wait --timeout=180s -- wget -qO- nginx
    
    if ($LASTEXITCODE -eq 0) {
        exit 0
    }
}

# If we get here, service connection failed
exit 1