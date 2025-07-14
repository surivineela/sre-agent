# Check if NetworkPolicy exists
try {
    kubectl get networkpolicy np -n ns1 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "NetworkPolicy 'np' does not exist in namespace 'ns1'"
        exit 1
    }
} catch {
    Write-Host "NetworkPolicy 'np' does not exist in namespace 'ns1'"
    exit 1
}

Write-Host "✅ NetworkPolicy 'np' exists in namespace 'ns1'"

# Functional test: Verify ingress traffic is not affected (pod in ns2 can reach pod in ns1)
Write-Host "Testing that ingress traffic from ns2 to ns1 is not affected..."
try {
    $ingressTest = kubectl exec -n ns2 curl-ns2 -- curl -s --connect-timeout 120s http://httpd-ns1.ns1.svc.cluster.local 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrEmpty($ingressTest)) {
        Write-Host "Failed to connect from ns2 to ns1 - NetworkPolicy should not restrict incoming traffic"
        exit 1
    }
} catch {
    Write-Host "Failed to connect from ns2 to ns1 - NetworkPolicy should not restrict incoming traffic"
    exit 1
}
Write-Host "✅ Ingress traffic from ns2 to ns1 is allowed as expected"

# Functional test: Test connectivity from ns1 to ns2
Write-Host "Testing connectivity from ns1 to ns2..."
try {
    $curlResult = kubectl exec -n ns1 curl-ns1 -- curl -s --connect-timeout 120s http://httpd-ns2.ns2.svc.cluster.local 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrEmpty($curlResult)) {
        Write-Host "Failed to connect from ns1 to ns2 - NetworkPolicy might be too restrictive"
        exit 1
    }
} catch {
    Write-Host "Failed to connect from ns1 to ns2 - NetworkPolicy might be too restrictive"
    exit 1
}
Write-Host "✅ Pods in ns1 can reach pods in ns2 as expected"

# Functional test: Try to connect to something outside ns2
Write-Host "Testing that connections outside ns2 are blocked..."
try {
    $externalResult = kubectl exec -n ns1 curl-ns1 -- curl -s --connect-timeout 10s https://kubernetes.io 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrEmpty($externalResult)) {
        Write-Host "NetworkPolicy should prevent connections to external sites, but connection succeeded"
        exit 1
    }
} catch {
    # Expected to fail, so this is good
}
Write-Host "✅ Pods in ns1 cannot reach external sites as expected"

# More comprehensive DNS resolution test
Write-Host "Testing DNS resolution for internal services..."
try {
    $dnsInternal = kubectl exec -n ns1 curl-ns1 -- nslookup kubernetes.default.svc.cluster.local 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "DNS resolution for internal services failed - NetworkPolicy might block DNS traffic"
        exit 1
    }
} catch {
    Write-Host "DNS resolution for internal services failed - NetworkPolicy might block DNS traffic"
    exit 1
}

Write-Host "Testing DNS resolution for external domains..."
try {
    $dnsExternal = kubectl exec -n ns1 curl-ns1 -- nslookup kubernetes.io 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "DNS resolution for external domains failed - NetworkPolicy might block DNS traffic"
        exit 1
    }
} catch {
    Write-Host "DNS resolution for external domains failed - NetworkPolicy might block DNS traffic"
    exit 1
}
Write-Host "✅ DNS resolution works as expected"

# All verifications passed
Write-Host "🎉 All verifications passed! NetworkPolicy is correctly configured."
exit 0
