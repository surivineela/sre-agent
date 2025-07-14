# Create namespace and a deployment with initial replicas
kubectl delete namespace scale-test --ignore-not-found
kubectl create namespace scale-test
kubectl create deployment web-app --image=nginx --replicas=1 -n scale-test

# Wait for initial deployment to be ready
for ($i = 1; $i -le 30; $i++) {
    $availableReplicas = kubectl get deployment web-app -n scale-test -o jsonpath='{.status.availableReplicas}' 2>$null
    if ($availableReplicas -eq "1") {
        exit 0
    }
    Start-Sleep 1
}
