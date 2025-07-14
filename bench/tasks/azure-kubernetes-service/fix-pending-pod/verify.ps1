# Configuration
$POD_NAME = "homepage-pod"
$NAMESPACE = "homepage-ns"

kubectl wait --for=condition=Ready pod/$POD_NAME -n $NAMESPACE --timeout=30s
if ($LASTEXITCODE -ne 0) {
    exit 1
} else {
    exit 0
}
