# Set API hostname (replace with your actual hostname)
API_HOSTNAME="http://localhost:5073"

# Get all threads and extract IDs
THREAD_IDS=$(curl --insecure -s -X GET "$API_HOSTNAME/api/v1/threads" -H "Content-Type: application/json" | jq -r '.value[].id')

# Loop through and delete each thread
for ID in $THREAD_IDS; do
    echo "Deleting thread $ID..."
    curl --insecure -X DELETE "$API_HOSTNAME/api/v1/threads/$ID"
    echo ""
done

echo "All threads deleted."
