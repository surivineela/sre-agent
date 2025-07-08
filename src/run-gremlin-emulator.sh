#!/bin/bash
set -euo pipefail

# Enhanced error handling function
error_exit() {
    echo "❌ ERROR: $1" >&2
    echo "📍 Script failed at line $2" >&2
    exit 1
}

# Trap errors and provide line numbers
trap 'error_exit "Unexpected error occurred" $LINENO' ERR

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GRAPH_FOLDER="graphs"
GRAPH_PATH="${SCRIPT_DIR}/GremlinEmulator/${GRAPH_FOLDER}"

echo "🔍 Checking directory: ${GRAPH_PATH}"
if [[ ! -d "${GRAPH_PATH}" ]]; then
  error_exit "Directory ${GRAPH_PATH} does not exist. Please ensure the GremlinEmulator/graphs directory is present." $LINENO
fi

# Generate dynamic configuration
echo "🔄 Generating dynamic configuration..."
cd "${SCRIPT_DIR}/GremlinEmulator"

if [[ ! -f "./generate-config.sh" ]]; then
    error_exit "generate-config.sh not found in ${SCRIPT_DIR}/GremlinEmulator" $LINENO
fi

if [[ ! -x "./generate-config.sh" ]]; then
    echo "⚠️  Making generate-config.sh executable..."
    chmod +x ./generate-config.sh
fi

echo "📂 Running configuration generation from: $(pwd)"
if ! ./generate-config.sh; then
    error_exit "Failed to generate configuration. Check generate-config.sh script." $LINENO
fi

cd "${SCRIPT_DIR}"

# function to convert Unix path to Windows path when using Git Bash/Cygwin
to_win_path() {
  if command -v cygpath >/dev/null 2>&1; then
    cygpath -w "$1"
  else
    echo "$1"
  fi
}

# prepare host paths for Docker
HOST_GRAPH_PATH="$(to_win_path "${GRAPH_PATH}")"
HOST_CONF_DIR="$(to_win_path "${SCRIPT_DIR}/GremlinEmulator")"

echo "🔧 Setting permissions on GremlinEmulator directory..."
if ! chmod -R a+w "${SCRIPT_DIR}/GremlinEmulator"; then
    error_exit "Failed to set permissions on ${SCRIPT_DIR}/GremlinEmulator" $LINENO
fi

echo "🗑️  Removing existing gremlin container if it exists..."
docker rm -f gremlin 2>/dev/null || echo "ℹ️  No existing gremlin container to remove"

# Define the Docker command as an array
DOCKER_COMMAND_ARRAY=(
  docker run -d --name gremlin
  --user "$(id -u):$(id -g)"
  -p 8182:8182
  -v "${HOST_GRAPH_PATH}:/opt/graphs"
  -v "${HOST_CONF_DIR}:/opt/gremlin-server/custom-conf"
  tinkerpop/gremlin-server:3.7.3
  //opt/gremlin-server/custom-conf/gremlin-server.yaml
)

echo "Running Docker command:"
# Print each part of the command array, quoting for display
printf "%q " "${DOCKER_COMMAND_ARRAY[@]}"
echo # Newline after printing the command

echo "🐳 Verifying Docker is running..."
if ! docker info >/dev/null 2>&1; then
    error_exit "Docker is not running or not accessible. Please start Docker Desktop." $LINENO
fi

echo "📁 Host paths being mounted:"
echo "   Graph path: ${HOST_GRAPH_PATH} -> /opt/graphs"
echo "   Config path: ${HOST_CONF_DIR} -> /opt/gremlin-server/custom-conf"

# Execute the command by expanding the array
echo "🚀 Starting Gremlin container..."
if ! "${DOCKER_COMMAND_ARRAY[@]}"; then
    error_exit "Failed to start Docker container. Check Docker logs above for details." $LINENO
fi

echo "Waiting for container to start..."
sleep 5

echo "🔍 Checking container status..."
if ! docker ps | grep -q gremlin; then
    echo "❌ Container failed to start or is not running"
    echo "📋 Container status:"
    docker ps -a --filter name=gremlin
    echo "📋 Docker logs for gremlin container:"
    docker logs gremlin 2>&1 || echo "Failed to get logs"
    error_exit "Gremlin container failed to start properly" $LINENO
fi

echo "✅ Container is running!"
echo "📋 Fetching Docker logs for gremlin container:"
if ! docker logs gremlin; then
    echo "⚠️  Failed to retrieve container logs"
fi

echo "📂 Listing contents of /opt/graphs in the container:"
if ! docker exec gremlin ls -l //opt/graphs; then
    echo "⚠️  Failed to list /opt/graphs contents"
fi

echo "📂 Listing contents of /opt/gremlin-server/custom-conf in the container:"
if ! docker exec gremlin ls -l //opt/gremlin-server/custom-conf; then
    echo "⚠️  Failed to list /opt/gremlin-server/custom-conf contents"
fi

echo "🎉 Gremlin emulator setup complete!"
echo "🌐 Gremlin server should be accessible at: http://localhost:8182"




