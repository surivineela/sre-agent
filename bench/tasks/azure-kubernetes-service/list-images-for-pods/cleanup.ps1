$ErrorActionPreference = "Stop"

$NAMESPACE = "list-images-for-pods"

kubectl delete namespace $NAMESPACE
