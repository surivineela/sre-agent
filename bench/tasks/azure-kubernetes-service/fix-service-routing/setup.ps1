# Create namespace and deployment with one set of labels
kubectl delete namespace web --ignore-not-found
kubectl create namespace web

# Create deployment with label app=nginx
kubectl create deployment nginx --image=nginx -n web
# kubectl label deployment nginx -n web app=nginx --overwrite

# Create service with different selector (app=web)
@"
apiVersion: v1
kind: Service
metadata:
  name: nginx
  namespace: web
spec:
  ports:
  - port: 80
    targetPort: 80
  selector:
    app: web  # Mismatched label - deployment has app=nginx
"@ | kubectl apply -f -

# Wait for deployment to be ready
for ($i = 1; $i -le 30; $i++) {
    $availableReplicas = kubectl get deployment nginx -n web -o jsonpath='{.status.availableReplicas}' 2>$null
    if ($availableReplicas -eq "1") {
        exit 0
    }
    Start-Sleep 1
}
