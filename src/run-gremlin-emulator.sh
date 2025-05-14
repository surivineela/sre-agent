#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GRAPH_FOLDER="graphs"
GRAPH_PATH="${SCRIPT_DIR}/GremlinEmulator/${GRAPH_FOLDER}"

if [[ ! -d "${GRAPH_PATH}" ]]; then
  echo "Directory ${GRAPH_PATH} does not exist."
  exit 1
fi

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

chmod -R a+w "${SCRIPT_DIR}/GremlinEmulator"

docker rm -f gremlin 2>/dev/null

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

# Execute the command by expanding the array
"${DOCKER_COMMAND_ARRAY[@]}"

echo "Waiting for container to start..."
sleep 5

echo "Fetching Docker logs for gremlin container:"
docker logs gremlin

echo "Listing contents of /opt/graphs in the container:"
docker exec gremlin ls -l //opt/graphs

echo "Listing contents of /opt/gremlin-server/custom-conf in the container:"
docker exec gremlin ls -l //opt/gremlin-server/custom-conf




