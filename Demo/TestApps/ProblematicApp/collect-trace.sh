#!/bin/bash
# Find the process ID of our .NET application
PID=$(ps aux | grep "ProblematicApp.dll" | grep -v grep | awk '{print $2}')

if [ -z "$PID" ]; then
    echo "Application process not found"
    exit 1
fi

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
echo "Collecting trace for process $PID"
mkdir -p /dumps
dotnet-trace collect -p $PID -o /dumps/trace_$TIMESTAMP.nettrace
