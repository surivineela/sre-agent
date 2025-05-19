#!/bin/sh
set -e # Exit on error

echo "[PROF_SCRIPT] Starting simplified .NET CPU profiling script."
# Argument 1 is now DURATION_SECONDS_ARG
DURATION_SECONDS_ARG="${1:-30}" # Default to 30s if no duration argument

# Use a temporary, unique directory for any tools we might install
DIAG_TOOLS_BASE_DIR="/tmp/diag-tools-$$"
mkdir -p "$DIAG_TOOLS_BASE_DIR"
export DOTNET_INSTALL_DIR="$DIAG_TOOLS_BASE_DIR/.dotnet-sdk"
DOTNET_TOOLS_DIR="$DIAG_TOOLS_BASE_DIR/.dotnet-tools"
mkdir -p "$DOTNET_TOOLS_DIR"

export PATH="$DOTNET_INSTALL_DIR:$DOTNET_TOOLS_DIR:$PATH"
export DOTNET_ROOT="$DOTNET_INSTALL_DIR"


TRACE_CMD=""
if [ -f "$DOTNET_TOOLS_DIR/dotnet-trace" ]; then
    TRACE_CMD="$DOTNET_TOOLS_DIR/dotnet-trace"
    echo "[PROF_SCRIPT] Found dotnet-trace in our tools directory: $TRACE_CMD"
elif command -v dotnet-trace >/dev/null 2>&1; then
    TRACE_CMD=$(command -v dotnet-trace)
    echo "[PROF_SCRIPT] Found dotnet-trace in system PATH: $TRACE_CMD"
fi

if [ -z "$TRACE_CMD" ]; then
    echo "[PROF_SCRIPT] dotnet-trace not found. Attempting to ensure .NET SDK and install tools."
    if ! dotnet --list-sdks 2>/dev/null | grep -q '.'; then
        echo "[PROF_SCRIPT] .NET SDK not found or no SDKs listed. Attempting to install .NET 8 SDK..."
        if ! command -v curl >/dev/null 2>&1; then
            echo "[PROF_SCRIPT] curl not found. Attempting to install..."
            if command -v apt-get >/dev/null 2>&1; then
                DEBIAN_FRONTEND=noninteractive apt-get update -yqq && apt-get install -yqq curl
                if [ $? -ne 0 ]; then echo "[PROF_SCRIPT] WARNING: Failed to install curl via apt-get."; else echo "[PROF_SCRIPT] curl installed via apt-get."; fi
            else
                echo "[PROF_SCRIPT] WARNING: apt-get not found. Cannot install curl. SDK download may fail."
            fi
        fi
        curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --install-dir "$DOTNET_INSTALL_DIR" --channel 8.0 --no-path
        if [ ! -f "$DOTNET_INSTALL_DIR/dotnet" ]; then
            echo "[PROF_SCRIPT] ERROR: Failed to install .NET SDK to $DOTNET_INSTALL_DIR."
            rm -rf "$DIAG_TOOLS_BASE_DIR" # Cleanup
            exit 1
        fi
        echo "[PROF_SCRIPT] .NET SDK installed to $DOTNET_INSTALL_DIR."
    else
        echo "[PROF_SCRIPT] .NET SDK (dotnet command) seems to be available."
        if [ "$DOTNET_ROOT" = "$DOTNET_INSTALL_DIR" ] && [ ! -f "$DOTNET_INSTALL_DIR/dotnet" ] && command -v dotnet >/dev/null 2>&1; then
             DETECTED_DOTNET_CMD=$(command -v dotnet)
             export DOTNET_ROOT=$(dirname "$DETECTED_DOTNET_CMD") # Heuristic
             echo "[PROF_SCRIPT] Using existing system SDK. Guessed DOTNET_ROOT: $DOTNET_ROOT."
        fi
    fi

    echo "[PROF_SCRIPT] Installing dotnet-trace to $DOTNET_TOOLS_DIR using $DOTNET_ROOT/dotnet..."
    "$DOTNET_ROOT/dotnet" tool install --tool-path "$DOTNET_TOOLS_DIR" dotnet-trace
    if [ ! -f "$DOTNET_TOOLS_DIR/dotnet-trace" ]; then
        echo "[PROF_SCRIPT] ERROR: Failed to install dotnet-trace to $DOTNET_TOOLS_DIR."
        rm -rf "$DIAG_TOOLS_BASE_DIR" # Cleanup
        exit 1
    fi
    TRACE_CMD="$DOTNET_TOOLS_DIR/dotnet-trace"
    echo "[PROF_SCRIPT] dotnet-trace installed: $TRACE_CMD"
fi

# --- PID Discovery ---
ACTUAL_PID=""
echo "[PROF_SCRIPT] Discovering .NET processes using '$TRACE_CMD ps'..."
PROCESS_LIST_OUTPUT=""
set +e # Temporarily disable exit on error for this command
PROCESS_LIST_OUTPUT=$($TRACE_CMD ps 2>&1)
PS_EXIT_CODE=$?
set -e # Re-enable exit on error

if [ $PS_EXIT_CODE -ne 0 ]; then
    echo "[PROF_SCRIPT] ERROR: '$TRACE_CMD ps' command itself failed with exit code $PS_EXIT_CODE. Output:"
    echo "$PROCESS_LIST_OUTPUT"
    rm -rf "$DIAG_TOOLS_BASE_DIR" # Cleanup
    exit 1
fi

FIRST_PROCESS_LINE=$(echo "$PROCESS_LIST_OUTPUT" | grep -vE '^\s*PID\s+COMMAND|No supported .NET processes were found' | head -n 1)
NO_PROCESS_MARKER="[PROF_SCRIPT_INFO] No debuggable .NET process found. Profiling not applicable for non-.NET or non-debuggable processes."

if [ -z "$FIRST_PROCESS_LINE" ]; then
    echo "$NO_PROCESS_MARKER"
    echo "[PROF_SCRIPT] Output from '$TRACE_CMD ps':"
    echo "$PROCESS_LIST_OUTPUT"
    rm -rf "$DIAG_TOOLS_BASE_DIR"
    exit 0
fi

ACTUAL_PID=$(echo "$FIRST_PROCESS_LINE" | awk '{print $1}')
echo "[PROF_SCRIPT] Auto-discovered PID: $ACTUAL_PID from process line: $FIRST_PROCESS_LINE"

if ! echo "$ACTUAL_PID" | grep -qE '^[0-9]+$'; then
    echo "[PROF_SCRIPT] ERROR: Invalid or non-numeric PID extracted: '$ACTUAL_PID' from line '$FIRST_PROCESS_LINE'."
    echo "[PROF_SCRIPT] Full '$TRACE_CMD ps' output:"
    echo "$PROCESS_LIST_OUTPUT"
    rm -rf "$DIAG_TOOLS_BASE_DIR"
    exit 1
fi

# --- Trace Collection ---
TRACE_FILENAME="cpu_profile_$(echo $HOSTNAME)_${ACTUAL_PID}_$(date +%s).nettrace"
TRACE_PATH_IN_POD="/tmp/$TRACE_FILENAME"

echo "[PROF_SCRIPT] Collecting CPU trace for PID $ACTUAL_PID for $DURATION_SECONDS_ARG seconds to $TRACE_PATH_IN_POD..."
$TRACE_CMD collect \
    --process-id "$ACTUAL_PID" \
    --duration "00:00:$DURATION_SECONDS_ARG" \
    --profile cpu-sampling \
    --format NetTrace \
    -o "$TRACE_PATH_IN_POD"

COLLECT_EXIT_CODE=$?
if [ $COLLECT_EXIT_CODE -ne 0 ] && [ $COLLECT_EXIT_CODE -ne 130 ]; then
    echo "[PROF_SCRIPT] WARNING: '$TRACE_CMD collect' exited with code $COLLECT_EXIT_CODE."
fi

if [ ! -f "$TRACE_PATH_IN_POD" ]; then
    echo "[PROF_SCRIPT] ERROR: Trace file $TRACE_PATH_IN_POD was not created. '$TRACE_CMD collect' may have failed."
    set +e; $TRACE_CMD ps 2>&1; set -e
    rm -rf "$DIAG_TOOLS_BASE_DIR"
    exit 1
fi
echo "[PROF_SCRIPT] Trace collection finished. File: $TRACE_PATH_IN_POD"

# --- Trace Analysis ---
TOP_N_COUNT=10 # Define how many top entries you want

# Report sorted by EXCLUSIVE time (default)
echo "[PROF_SCRIPT] Analyzing trace file $TRACE_PATH_IN_POD for Top ${TOP_N_COUNT} methods (sorted by EXCLUSIVE time)..."
ANALYSIS_EXCLUSIVE_STDOUT_STDERR=""
REPORT_EXCLUSIVE_EXIT_CODE=0
set +e
ANALYSIS_EXCLUSIVE_STDOUT_STDERR=$($TRACE_CMD report "$TRACE_PATH_IN_POD" topN -n "$TOP_N_COUNT" 2>&1)
REPORT_EXCLUSIVE_EXIT_CODE=$?
set -e

# Report sorted by INCLUSIVE time (using --inclusive flag)
echo "[PROF_SCRIPT] Analyzing trace file $TRACE_PATH_IN_POD for Top ${TOP_N_COUNT} methods (sorted by INCLUSIVE time)..."
ANALYSIS_INCLUSIVE_STDOUT_STDERR=""
REPORT_INCLUSIVE_EXIT_CODE=0
set +e
ANALYSIS_INCLUSIVE_STDOUT_STDERR=$($TRACE_CMD report "$TRACE_PATH_IN_POD" topN -n "$TOP_N_COUNT" --inclusive 2>&1)
REPORT_INCLUSIVE_EXIT_CODE=$?
set -e

# --- Cleanup ---
echo "[PROF_SCRIPT] Cleaning up trace file $TRACE_PATH_IN_POD from /tmp..."
rm -f "$TRACE_PATH_IN_POD"
echo "[PROF_SCRIPT] Cleaning up temporary diagnostic tools directory $DIAG_TOOLS_BASE_DIR..."
rm -rf "$DIAG_TOOLS_BASE_DIR"

# --- Final Output ---
FINAL_REPORT_HAS_ERRORS=0
FINAL_REPORT_OUTPUT=""

if [ $REPORT_EXCLUSIVE_EXIT_CODE -ne 0 ]; then
    FINAL_REPORT_HAS_ERRORS=1
    FINAL_REPORT_OUTPUT="${FINAL_REPORT_OUTPUT}[PROF_SCRIPT] ERROR: '$TRACE_CMD report topN (exclusive)' failed with exit code $REPORT_EXCLUSIVE_EXIT_CODE. Output/Error:\n$ANALYSIS_EXCLUSIVE_STDOUT_STDERR\n\n"
else
    FINAL_REPORT_OUTPUT="${FINAL_REPORT_OUTPUT}Top ${TOP_N_COUNT} CPU methods (sorted by EXCLUSIVE time - default):\n$ANALYSIS_EXCLUSIVE_STDOUT_STDERR\n\n"
fi

if [ $REPORT_INCLUSIVE_EXIT_CODE -ne 0 ]; then
    FINAL_REPORT_HAS_ERRORS=1
    FINAL_REPORT_OUTPUT="${FINAL_REPORT_OUTPUT}[PROF_SCRIPT] ERROR: '$TRACE_CMD report topN --inclusive' failed with exit code $REPORT_INCLUSIVE_EXIT_CODE. Output/Error:\n$ANALYSIS_INCLUSIVE_STDOUT_STDERR\n"
else
    FINAL_REPORT_OUTPUT="${FINAL_REPORT_OUTPUT}Top ${TOP_N_COUNT} CPU methods (sorted by INCLUSIVE time):\n$ANALYSIS_INCLUSIVE_STDOUT_STDERR\n"
fi

echo "[PROF_SCRIPT] CPU Profiling script completed. Analysis result follows:"
echo "-------------------- ANALYSIS START --------------------"
# Use printf for better handling of newlines in the output variable
printf "%b" "$FINAL_REPORT_OUTPUT"
echo "-------------------- ANALYSIS END ----------------------"

if [ $FINAL_REPORT_HAS_ERRORS -ne 0 ]; then
    exit 1 # Signal failure
fi
exit 0