# Delete the namespace which will also delete all resources in it
kubectl delete namespace limits-test --ignore-not-found
Write-Host "Cleanup completed"
