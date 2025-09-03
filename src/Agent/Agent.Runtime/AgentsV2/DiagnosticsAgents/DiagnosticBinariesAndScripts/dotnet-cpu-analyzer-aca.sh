set -e
export TERM=xterm
export COLUMNS=120
export LINES=30
BASE_DIR="$(pwd)/diag-tools"
mkdir -p "$BASE_DIR"
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --install-dir "$BASE_DIR/.dotnet"
export DOTNET_ROOT="$BASE_DIR/.dotnet"
export PATH="$DOTNET_ROOT:$BASE_DIR:$PATH"
"$DOTNET_ROOT/dotnet" tool install --tool-path "$BASE_DIR" dotnet-trace
echo ">>COMPLETED DOWNLOAD<<"
"$BASE_DIR/dotnet-trace" collect -p 1 -o "$BASE_DIR/trace.nettrace" --duration 00:00:00:30
echo ">>STARTED ANALYSIS<<"
"$BASE_DIR/dotnet-trace" report "$BASE_DIR/trace.nettrace" topN -n 100 --inclusive --verbose
echo ">>COMPLETED ANALYSIS<<"