# Create namespace and a deployment with initial replicas
kubectl delete namespace scale-down-test --ignore-not-found
kubectl create namespace scale-down-test
kubectl create deployment web-service --image=nginx --replicas=4 -n scale-down-test

# Wait for initial deployment to be ready
for ($i = 1; $i -le 30; $i++) {
    $availableReplicas = kubectl get deployment web-service -n scale-down-test -o jsonpath='{.status.availableReplicas}' 2>$null
    if ($availableReplicas -eq "4") {
        exit 0
    }
    Start-Sleep 1
}
