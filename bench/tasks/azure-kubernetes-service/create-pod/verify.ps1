# Wait for pod to be running with kubectl wait
if (kubectl wait --for=condition=Ready pod/web-server -n create-pod-test --timeout=30s) {
   if ($LASTEXITCODE -eq 0) {
       exit 0
   } else {
       exit 1
   }
} else {
   # If we get here, pod didn't reach Running state in time
   exit 1
}