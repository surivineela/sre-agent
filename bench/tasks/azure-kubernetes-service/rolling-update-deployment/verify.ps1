# Wait for rollout to complete
kubectl rollout status deployment/web-app -n rollout-test --timeout=120s
if ($LASTEXITCODE -ne 0) {
    exit 1
}

# Verify each pod is running the new image
$pods = kubectl get pods -n rollout-test -l app=web-app -o jsonpath='{.items[*].spec.containers[0].image}'
$podImages = $pods -split ' '
foreach ($img in $podImages) {
    if ($img -ne "nginx:1.22") {
        exit 1
    }
}

exit 0
