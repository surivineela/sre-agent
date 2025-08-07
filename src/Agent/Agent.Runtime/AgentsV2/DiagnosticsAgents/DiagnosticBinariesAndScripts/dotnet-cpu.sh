#!/bin/bash
set -e

export BASE_DIR="/home"
mkdir -p "$BASE_DIR"
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --install-dir "$BASE_DIR/.dotnet"
export DOTNET_ROOT="$BASE_DIR/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
"$DOTNET_ROOT/dotnet" tool install dotnet-trace --tool-path "$BASE_DIR"/tools
export PATH="$BASE_DIR:$PATH"
echo ">>STARTING ANALYSIS<<"
tools/dotnet-trace report $1 topN -n 100 --inclusive --verbose
echo ">>COMPLETED ANALYSIS<<"
