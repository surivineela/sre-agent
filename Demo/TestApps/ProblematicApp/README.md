# Performance Issues Demo Application

A Simple that emulates memory leaks and CPU-intensive operations for testing/debugging purposes.

## Quick Start

```bash
docker build . -t problem-app
docker run -p 5877:5877 problem-app
```

Access Swagger UI: http://localhost:5877/swagger

## Memory Leak Testing

### 1. Leaky Cache
```http
POST /memory/cache
{
    "sizeMB": 100
}
```
- Call repeatedly to accumulate data in memory
- Each call stores X MB in static cache
- Monitor via `GET /memory/cache/size`

### 2. Event Subscribers
```http
POST /memory/subscribe
```
- Each call creates non-disposable event handlers
- Call repeatedly to accumulate handlers

### 3. Data Generation
```http
POST /memory/generate-data
{
    "recordCount": 10000
}
```
- Generates and stores data in memory indefinitely
- Large recordCount values accelerate memory growth

## CPU Load Testing

### 1. Start CPU Task
```http
POST /cpu/start
{
    "complexity": 50000
}
```
- Returns taskId
- Higher complexity = more CPU usage

### 2. Manage Tasks
```http
GET /cpu/active                  # List running tasks
POST /cpu/stop/{taskId}         # Stop specific task
POST /cpu/stop-all              # Stop all tasks
```

## Collecting Diagnostics

### Memory Dumps
The test app contains a dotnet-dump binary for linux-x64, that exposes a attach subcommand, which could be used to dump memory stats directly without needing to dump to a file first.
We are assuming that the process id of the dotnet process is 1, which is true in simple cases.
```bash
./dotnet-dump attach -p 1 < dotnet_dump_cmd.txt
```

### Performance Traces
```bash
./scripts/collect-trace.sh
```

Dumps and traces are saved in `./dumps` directory.

## Common Test Scenarios

1. **Memory Leak Test**:
   - Call `/memory/generate-data` and see container memory go up linearly
   - Monitor memory growth in container

2. **CPU Spike Test**:
   - Start multiple CPU tasks with complexity 50000+
   - Monitor CPU usage in container

3. **Combined Load**:
   - Run memory leak and CPU tests simultaneously
   - Generate data while CPU tasks are running