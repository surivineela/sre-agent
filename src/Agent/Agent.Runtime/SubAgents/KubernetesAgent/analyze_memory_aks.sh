#!/bin/sh
set -e # Exit on error

echo "[MEM_ANALYSIS_SCRIPT] Starting .NET memory analysis script."

# Use a temporary, unique directory for tools and dumps
DIAG_TOOLS_BASE_DIR="/tmp/diag-tools-$$"
mkdir -p "$DIAG_TOOLS_BASE_DIR"
export DOTNET_INSTALL_DIR="$DIAG_TOOLS_BASE_DIR/.dotnet-sdk"
DOTNET_TOOLS_DIR="$DIAG_TOOLS_BASE_DIR/.dotnet-tools"
mkdir -p "$DOTNET_TOOLS_DIR"

export PATH="$DOTNET_INSTALL_DIR:$DOTNET_TOOLS_DIR:$PATH"
export DOTNET_ROOT="$DOTNET_INSTALL_DIR"

# --- Tool Discovery/Installation (dotnet-trace for 'ps', dotnet-dump for 'collect') ---
TRACE_CMD=""
DUMP_CMD=""

# Check for curl first, as it's needed for SDK and analyzer download
if ! command -v curl >/dev/null 2>&1; then
    echo "[MEM_ANALYSIS_SCRIPT] curl not found. Attempting to install..."
    if command -v apt-get >/dev/null 2>&1; then
        DEBIAN_FRONTEND=noninteractive apt-get update -yqq && apt-get install -yqq apt-utils && apt-get install -yqq curl
        if [ $? -ne 0 ]; then echo "[MEM_ANALYSIS_SCRIPT] WARNING: Failed to install curl via apt-get."; else echo "[MEM_ANALYSIS_SCRIPT] curl installed via apt-get."; fi
    elif command -v apk >/dev/null 2>&1; then
        apk add --no-cache curl
        if [ $? -ne 0 ]; then echo "[MEM_ANALYSIS_SCRIPT] WARNING: Failed to install curl via apk."; else echo "[MEM_ANALYSIS_SCRIPT] curl installed via apk."; fi
    else
        echo "[MEM_ANALYSIS_SCRIPT] ERROR: curl, apt-get, and apk not found. Cannot proceed."
        rm -rf "$DIAG_TOOLS_BASE_DIR"
        exit 1
    fi
fi
if ! command -v curl >/dev/null 2>&1; then
    echo "[MEM_ANALYSIS_SCRIPT] ERROR: curl could not be installed. Cannot proceed."
    rm -rf "$DIAG_TOOLS_BASE_DIR"
    exit 1
fi


# Ensure .NET SDK is available (for 'dotnet tool install')
if ! dotnet --list-sdks 2>/dev/null | grep -q '.'; then
    echo "[MEM_ANALYSIS_SCRIPT] .NET SDK not found. Attempting to install .NET 8 SDK..."
    curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --install-dir "$DOTNET_INSTALL_DIR" --channel 8.0 --no-path
    if [ ! -f "$DOTNET_INSTALL_DIR/dotnet" ]; then
        echo "[MEM_ANALYSIS_SCRIPT] ERROR: Failed to install .NET SDK to $DOTNET_INSTALL_DIR."
        rm -rf "$DIAG_TOOLS_BASE_DIR" # Cleanup
        exit 1
    fi
    echo "[MEM_ANALYSIS_SCRIPT] .NET SDK installed to $DOTNET_INSTALL_DIR."
else
    echo "[MEM_ANALYSIS_SCRIPT] .NET SDK (dotnet command) seems to be available."
    if [ "$DOTNET_ROOT" = "$DOTNET_INSTALL_DIR" ] && [ ! -f "$DOTNET_INSTALL_DIR/dotnet" ] && command -v dotnet >/dev/null 2>&1; then
         DETECTED_DOTNET_CMD=$(command -v dotnet)
         export DOTNET_ROOT=$(dirname "$DETECTED_DOTNET_CMD")
         echo "[MEM_ANALYSIS_SCRIPT] Using existing system SDK. Guessed DOTNET_ROOT: $DOTNET_ROOT."
    fi
fi

# Install dotnet-trace
if [ -f "$DOTNET_TOOLS_DIR/dotnet-trace" ]; then
    TRACE_CMD="$DOTNET_TOOLS_DIR/dotnet-trace"
elif command -v dotnet-trace >/dev/null 2>&1; then
    TRACE_CMD=$(command -v dotnet-trace)
else
    echo "[MEM_ANALYSIS_SCRIPT] Installing dotnet-trace to $DOTNET_TOOLS_DIR..."
    "$DOTNET_ROOT/dotnet" tool install --tool-path "$DOTNET_TOOLS_DIR" dotnet-trace
    if [ ! -f "$DOTNET_TOOLS_DIR/dotnet-trace" ]; then echo "[MEM_ANALYSIS_SCRIPT] ERROR: Failed to install dotnet-trace."; rm -rf "$DIAG_TOOLS_BASE_DIR"; exit 1; fi
    TRACE_CMD="$DOTNET_TOOLS_DIR/dotnet-trace"
fi
echo "[MEM_ANALYSIS_SCRIPT] Using dotnet-trace: $TRACE_CMD"

# Install dotnet-dump
if [ -f "$DOTNET_TOOLS_DIR/dotnet-dump" ]; then
    DUMP_CMD="$DOTNET_TOOLS_DIR/dotnet-dump"
elif command -v dotnet-dump >/dev/null 2>&1; then
    DUMP_CMD=$(command -v dotnet-dump)
else
    echo "[MEM_ANALYSIS_SCRIPT] Installing dotnet-dump to $DOTNET_TOOLS_DIR..."
    "$DOTNET_ROOT/dotnet" tool install --tool-path "$DOTNET_TOOLS_DIR" dotnet-dump
    if [ ! -f "$DOTNET_TOOLS_DIR/dotnet-dump" ]; then echo "[MEM_ANALYSIS_SCRIPT] ERROR: Failed to install dotnet-dump."; rm -rf "$DIAG_TOOLS_BASE_DIR"; exit 1; fi
    DUMP_CMD="$DOTNET_TOOLS_DIR/dotnet-dump"
fi
echo "[MEM_ANALYSIS_SCRIPT] Using dotnet-dump: $DUMP_CMD"

# --- PID Discovery ---
ACTUAL_PID=""
echo "[MEM_ANALYSIS_SCRIPT] Discovering .NET processes using '$TRACE_CMD ps'..."
PROCESS_LIST_OUTPUT=""
set +e # Temporarily disable exit on error
PROCESS_LIST_OUTPUT=$($TRACE_CMD ps 2>&1)
PS_EXIT_CODE=$?
set -e # Re-enable

if [ $PS_EXIT_CODE -ne 0 ]; then
    echo "[MEM_ANALYSIS_SCRIPT] ERROR: '$TRACE_CMD ps' command failed. Output:"
    echo "$PROCESS_LIST_OUTPUT"
    rm -rf "$DIAG_TOOLS_BASE_DIR"
    exit 1
fi

NO_PROCESS_MARKER="[MEM_ANALYSIS_SCRIPT_INFO] No debuggable .NET process found. Memory analysis not applicable."
FIRST_PROCESS_LINE=$(echo "$PROCESS_LIST_OUTPUT" | grep -vE '^\s*PID\s+COMMAND|No supported .NET processes were found|Waiting for connections|determine process operating system' | head -n 1)

if [ -z "$FIRST_PROCESS_LINE" ]; then
    echo "$NO_PROCESS_MARKER"
    echo "[MEM_ANALYSIS_SCRIPT] Output from '$TRACE_CMD ps':"
    echo "$PROCESS_LIST_OUTPUT"
    rm -rf "$DIAG_TOOLS_BASE_DIR"
    exit 0 # Not an error, just no process to analyze
fi

ACTUAL_PID=$(echo "$FIRST_PROCESS_LINE" | awk '{print $1}')
echo "[MEM_ANALYSIS_SCRIPT] Auto-discovered PID: $ACTUAL_PID from process line: $FIRST_PROCESS_LINE"

if ! echo "$ACTUAL_PID" | grep -qE '^[0-9]+$'; then
    echo "[MEM_ANALYSIS_SCRIPT] ERROR: Invalid PID extracted: '$ACTUAL_PID'."
    echo "[MEM_ANALYSIS_SCRIPT] Full '$TRACE_CMD ps' output:"
    echo "$PROCESS_LIST_OUTPUT"
    rm -rf "$DIAG_TOOLS_BASE_DIR"
    exit 1
fi

# --- Dump Collection ---
DUMP_FILENAME="memory_dump_$(echo $HOSTNAME)_${ACTUAL_PID}_$(date +%s).dmp"
DUMP_PATH_IN_POD="$DIAG_TOOLS_BASE_DIR/$DUMP_FILENAME"

echo "[MEM_ANALYSIS_SCRIPT] Collecting memory dump for PID $ACTUAL_PID to $DUMP_PATH_IN_POD..."
# Use --type Full for more comprehensive analysis, Mini for quicker/smaller. Default is Full with recent dotnet-dump.
# Adding --diag to get more logs from dotnet-dump if it fails
set +e # dotnet-dump can have non-zero exit codes even on success in some older versions or specific scenarios
$DUMP_CMD collect --process-id "$ACTUAL_PID" -o "$DUMP_PATH_IN_POD"
COLLECT_EXIT_CODE=$?
set -e

if [ $COLLECT_EXIT_CODE -ne 0 ]; then
    echo "[MEM_ANALYSIS_SCRIPT] WARNING: '$DUMP_CMD collect' exited with code $COLLECT_EXIT_CODE."
    # Check if dump was created despite non-zero exit code
    if [ ! -f "$DUMP_PATH_IN_POD" ] || [ ! -s "$DUMP_PATH_IN_POD" ]; then # Check if file exists and is not empty
        echo "[MEM_ANALYSIS_SCRIPT] ERROR: Dump file $DUMP_PATH_IN_POD was not created or is empty. '$DUMP_CMD collect' failed."
        echo "[MEM_ANALYSIS_SCRIPT] Checking current processes again:"
        set +e; $TRACE_CMD ps 2>&1; set -e
        rm -rf "$DIAG_TOOLS_BASE_DIR"
        exit 1
    else
        echo "[MEM_ANALYSIS_SCRIPT] Dump file created despite non-zero exit code. Proceeding with analysis."
    fi
elif [ ! -f "$DUMP_PATH_IN_POD" ] || [ ! -s "$DUMP_PATH_IN_POD" ]; then
     echo "[MEM_ANALYSIS_SCRIPT] ERROR: Dump file $DUMP_PATH_IN_POD was not created or is empty even with exit code 0."
     rm -rf "$DIAG_TOOLS_BASE_DIR"
     exit 1
fi
echo "[MEM_ANALYSIS_SCRIPT] Memory dump collection finished. File: $DUMP_PATH_IN_POD"

# --- Download Analyzer ---
ANALYZER_PATH="$DIAG_TOOLS_BASE_DIR/dotnetanalyzer"
echo "[MEM_ANALYSIS_SCRIPT] Downloading analyzer to $ANALYZER_PATH..."
curl -sSL https://dotnetanalysis.blob.core.windows.net/lin64/DotnetAnalyzer -o "$ANALYZER_PATH"
if [ $? -ne 0 ] || [ ! -f "$ANALYZER_PATH" ]; then
    echo "[MEM_ANALYSIS_SCRIPT] ERROR: Failed to download analyzer."
    rm -rf "$DIAG_TOOLS_BASE_DIR"
    exit 1
fi
chmod +x "$ANALYZER_PATH"
echo "[MEM_ANALYSIS_SCRIPT] Analyzer downloaded."

# --- Memory Analysis ---
echo "[MEM_ANALYSIS_SCRIPT] Starting memory analysis of $DUMP_PATH_IN_POD..."
echo "-------------------- ANALYSIS START --------------------"
# Run analyzer. It will output to stdout.
"$ANALYZER_PATH" analyze-memory "$DUMP_PATH_IN_POD"
ANALYSIS_EXIT_CODE=$?
echo "-------------------- ANALYSIS END ----------------------"

if [ $ANALYSIS_EXIT_CODE -ne 0 ]; then
    echo "[MEM_ANALYSIS_SCRIPT] WARNING: Analyzer exited with code $ANALYSIS_EXIT_CODE."
    # The output is already captured between markers, so just log this warning.
fi

# --- Cleanup ---
echo "[MEM_ANALYSIS_SCRIPT] Cleaning up dump file $DUMP_PATH_IN_POD..."
rm -f "$DUMP_PATH_IN_POD"
echo "[MEM_ANALYSIS_SCRIPT] Cleaning up temporary diagnostic tools directory $DIAG_TOOLS_BASE_DIR..."
rm -rf "$DIAG_TOOLS_BASE_DIR"

echo "[MEM_ANALYSIS_SCRIPT] Memory analysis script completed."
exit 0 # Success, even if analyzer had warnings, as long as script ran.