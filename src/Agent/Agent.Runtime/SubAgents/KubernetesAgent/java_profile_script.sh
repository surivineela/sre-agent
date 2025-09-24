#!/bin/sh

set -e

# Define the path to the diagnosis directory and files
DIAGNOSIS_DIR="/tmp/illuminate"
DIAGNOSIS_FILE="${DIAGNOSIS_DIR}/diagnosis.json"
METADATA_FILE="${DIAGNOSIS_DIR}/diagnosis-metadata.json"
REPORT_FILE="${DIAGNOSIS_DIR}/diagnosis-report.md"

# First check if the directory exists
if [ ! -d "$DIAGNOSIS_DIR" ]; then
    echo "[PROF_SCRIPT] ERROR: Directory $DIAGNOSIS_DIR does not exist. This indicates Illuminate has not been executed." >&2
    exit 2
fi

# Check if both files exist
if [ -f "$DIAGNOSIS_FILE" ] && [ -f "$METADATA_FILE" ] && [ -f "$REPORT_FILE" ]; then

    # Check if diagnosis files are recent (updated within last 10 minutes)
    CURRENT_TIME=$(date +%s)
    TEN_MINUTES_AGO=$((CURRENT_TIME - 600))  # 600 seconds = 10 minutes

    DIAGNOSIS_MTIME=$(stat -c %Y "$DIAGNOSIS_FILE" 2>/dev/null || stat -f %m "$DIAGNOSIS_FILE" 2>/dev/null || echo 0)
    if [ "$DIAGNOSIS_MTIME" -lt "$TEN_MINUTES_AGO" ]; then
        echo "[PROF_SCRIPT] ERROR: Diagnosis file $DIAGNOSIS_FILE is older than 10 minutes (last modified: $(date -d @$DIAGNOSIS_MTIME 2>/dev/null || date -r $DIAGNOSIS_MTIME 2>/dev/null || echo 'unknown'))." >&2
        echo "[PROF_SCRIPT] This indicates the diagnosis data is stale and may not reflect current application state." >&2
        exit 3
    fi

    # Since files exist, print them all
    echo "[PROF_SCRIPT] CPU Profiling script completed. Analysis result follows:"
    echo "-------------------- ANALYSIS START --------------------"
    cat "$DIAGNOSIS_FILE"
    echo "-------------------- ANALYSIS END ----------------------"

    echo ""
    echo "[PROF_SCRIPT] Diagnosis metadata information:"
    echo "-------------------- METADATA START --------------------"
    cat "$METADATA_FILE"
    echo "-------------------- METADATA END ----------------------"

    echo ""
    echo "[PROF_SCRIPT] Detailed diagnosis report:"
    echo "-------------------- DIAGNOSIS REPORT START ----------------------"
    cat "$REPORT_FILE"
    echo "-------------------- DIAGNOSIS REPORT END ----------------------"

    exit 0
else
    # At least one file is missing
    if [ ! -f "$DIAGNOSIS_FILE" ]; then
        echo "[PROF_SCRIPT] ERROR: Diagnosis file not found at $DIAGNOSIS_FILE" >&2
    fi

    if [ ! -f "$METADATA_FILE" ]; then
        echo "[PROF_SCRIPT] ERROR: Metadata file not found at $METADATA_FILE" >&2
    fi

    if [ ! -f "$REPORT_FILE" ]; then
        echo "[PROF_SCRIPT] WARNING: Report file not found at $REPORT_FILE" >&2
    fi

    echo "[PROF_SCRIPT] ERROR: diagnosis-report.md, diagnosis.json and diagnosis-metadata.json must be present." >&2
    exit 1
fi
