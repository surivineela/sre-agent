# Initialize namespace and deployment with the old image
kubectl delete namespace rollout-test --ignore-not-found
kubectl create namespace rollout-test
kubectl create deployment web-app --image=nginx:1.21 --replicas=3 -n rollout-test

# Wait until all replicas are available
kubectl wait deployment/web-app -n rollout-test --for=condition=Available=True --timeout=60s
if ($LASTEXITCODE -eq 0) {
    exit 0
} else {
    Write-Host "Initial deployment did not become ready in time"
    exit 1
}
