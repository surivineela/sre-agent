# Delete the namespace which will remove all resources created for this task
kubectl delete namespace health-check --ignore-not-found

Write-Host "Cleanup completed"
