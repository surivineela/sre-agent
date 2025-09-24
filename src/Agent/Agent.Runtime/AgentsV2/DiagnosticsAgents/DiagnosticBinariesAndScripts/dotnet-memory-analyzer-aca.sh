set -e
export TERM=xterm
export COLUMNS=120
export LINES=30
BASE_DIR="/app/diag-tools"
DUMP_FILE="$BASE_DIR/dump.dmp"
HEAPSTATFILE="$BASE_DIR/heapstat.txt"
LARGESTHEAPSTAT="$BASE_DIR/heapstat_highest.txt"
ADDRESSES="$BASE_DIR/addresses.txt"
SAMPLED_ADDRESSES="$BASE_DIR/sample.txt"
OUTPUT_FILE="$BASE_DIR/gcroot_sampled_results.txt"
FILTERED_OUTPUT_FILE="$BASE_DIR/gcroot_filtered.txt"
CHAINS_ONLY_FILE="$BASE_DIR/root_paths.txt"
AGGREGATED_CHAINS="$BASE_DIR/aggregated_chains.txt"
mkdir -p "$BASE_DIR"
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --install-dir "$BASE_DIR/.dotnet"
export DOTNET_ROOT="$BASE_DIR/.dotnet"
export PATH="$DOTNET_ROOT:$BASE_DIR:$PATH"
"$DOTNET_ROOT/dotnet" tool install --tool-path "$BASE_DIR" dotnet-dump
dotnet-dump collect -p 1 -o "$BASE_DIR/dump.dmp"
dotnet-dump analyze "$BASE_DIR/dump.dmp" --command "dumpheap -stat" --command "exit" > "$HEAPSTATFILE"
$HEAPSTATSFILE | tail -n2 $HEAPSTATFILE | head -n1 > $LARGESTHEAPSTAT
MT=$(cat $LARGESTHEAPSTAT | awk '{print $1}')
dotnet-dump analyze "$BASE_DIR/dump.dmp" --command "dumpheap -mt $MT" --command "exit" > "$ADDRESSES"
awk 'NF == 3 { print $1 }' "$ADDRESSES" | shuf | head -n 100 > "$SAMPLED_ADDRESSES"
dotnet-dump analyze "$DUMP_FILE" > "$OUTPUT_FILE" < <(
    while read -r address; do
        [[ -n "$address" ]] && echo "gcroot $address"
    done < "$SAMPLED_ADDRESSES"
    echo "exit"
)
awk '/^> gcroot /{cmd=$0;next} /^Found 0 unique roots\./{skip=1;next} /^<END_COMMAND_OUTPUT>/{if(skip){skip=0;next}else{print cmd;print} next} {if(!skip){if(cmd){print cmd; cmd=""} print}}' "$OUTPUT_FILE" > "$FILTERED_OUTPUT_FILE"
awk '/^ *->/ { gsub(/^ *-> *[0-9a-f]+ +/, ""); path = (path ? path " -> " : "") $0; next } /^$/ { if (path) print path; path = "" } END { if (path) print path }' "$FILTERED_OUTPUT_FILE" > "$CHAINS_ONLY_FILE"
sort $CHAINS_ONLY_FILE | uniq -c | sort -nr | head -n 20 > $AGGREGATED_CHAINS
echo  ">>STARTED ANALYSIS<<"
echo "Heap Stats Info about the type occupying the most space: "
cat $LARGESTHEAPSTAT
echo "GC Roots: "
cat $AGGREGATED_CHAINS
echo ">>COMPLETED ANALYSIS<<"
