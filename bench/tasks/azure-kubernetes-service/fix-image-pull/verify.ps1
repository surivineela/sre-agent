# Wait for pod to be ready
kubectl wait --for=condition=Ready pod -l app=nginx -n debug --timeout=25s
if ($LASTEXITCODE -eq 0) {
    # Get current restart count
    $restarts = kubectl get pods -n debug -l app=nginx -o jsonpath='{.items[0].status.containerStatuses[0].restartCount}'
    
    # Wait additional 5 seconds to ensure stability
    Start-Sleep 5
    
    # Check if restart count hasn't increased
    $newRestarts = kubectl get pods -n debug -l app=nginx -o jsonpath='{.items[0].status.containerStatuses[0].restartCount}'
    if ($restarts -eq $newRestarts) {
        exit 0
    }
}

# If we get here, pod didn't stabilize in time
exit 1
