# Wait until HPA scales above 1 replica
kubectl wait hpa/web-app -n hpa-test --for=condition=ScalingActive --timeout=120s
if ($LASTEXITCODE -eq 0) {
    exit 0
} else {
    Write-Host "HPA did not scale above 1 replica in time"
    exit 1
}
