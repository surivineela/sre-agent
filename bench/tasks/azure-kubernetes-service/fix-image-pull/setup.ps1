# Create namespace and a deployment with an invalid image that will cause ImagePullBackOff
kubectl delete namespace debug --ignore-not-found
kubectl create namespace debug

@"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: app
  namespace: debug
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
        image: nginx:invalid-tag  # This will cause ImagePullBackOff error
"@ | kubectl apply -f -

# Wait for deployment's pod to enter ImagePullBackOff state
for ($i = 1; $i -le 30; $i++) {
    $reason = kubectl get pods -n debug -l app=nginx -o jsonpath='{.items[0].status.containerStatuses[0].state.waiting.reason}' 2>$null
    if ($reason -eq "ImagePullBackOff") {
        exit 0
    }
    Start-Sleep 1
}
