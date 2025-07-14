# Wait for deployment to scale to 2 replicas with kubectl wait
kubectl wait --for=condition=Available=True --timeout=30s deployment/web-app -n scale-test
if ($LASTEXITCODE -eq 0) {
    # Verify the replica count is exactly 2
    $availableReplicas = kubectl get deployment web-app -n scale-test -o jsonpath='{.status.availableReplicas}'
    if ($availableReplicas -eq "2") {
        exit 0
    }
}

# If we get here, deployment didn't scale up correctly in time
exit 1
