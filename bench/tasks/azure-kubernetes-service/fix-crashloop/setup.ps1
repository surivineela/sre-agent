kubectl delete namespace crashloop-test --ignore-not-found
# Create namespace and a deployment with an invalid command that will cause crashloop
kubectl create namespace crashloop-test

@"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: app
  namespace: crashloop-test
spec:
  replicas: 1
  selector:
    matchLabels:
      app: nginx
  template:
    metadata:
      labels:
        app: nginx
    spec:
      containers:
      - name: nginx
        image: nginx
        command: ["/bin/sh", "-c"]
        args: ["nonexistent_command"]  # This will cause the pod to crash
"@ | kubectl apply -f -

# Wait for pod to enter crashloop state
for ($i = 1; $i -le 30; $i++) {
    $restartCount = kubectl get pods -n crashloop-test -l app=nginx -o jsonpath='{.items[0].status.containerStatuses[0].restartCount}' 2>$null
    if ($restartCount -and [int]$restartCount -gt 0) {
        exit 0
    }
    Start-Sleep 1
}
