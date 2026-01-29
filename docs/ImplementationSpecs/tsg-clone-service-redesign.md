# TsgConnectorCloneService Redesign

## Problem Statement

The original `TsgConnectorCloneService` had several issues:
1. **Sequential processing** - clones happened one at a time via a Channel queue
2. **Broken timeout detection** - git operations could hang indefinitely
3. **Overly eager periodic sync** - synced every hour regardless of actual need
4. **Direct process management** - didn't leverage existing `TerminalSessionManager`
5. **IServiceProvider dependency** - used scoped resolution instead of direct injection

## Design Goals

1. **Concurrent cloning** - up to 3 simultaneous clone/sync operations
2. **Batch-with-debounce** - process all connectors at once, rerun if requested while running
3. **Smart periodic sync** - based on last sync time (24h stale threshold), hourly check
4. **Use TerminalSessionManager** - leverage existing terminal infrastructure with new Guid overloads
5. **Git credential-store** - store PAT so LLM git operations work transparently
6. **Auth failure detection** - mark connectors with expired PATs as Error status

---

## Architecture

### Batch-with-Debounce Pattern

Instead of a Channel-based per-connector queue, the service uses a simple lock + flag pattern:

```
QueueCodeRepositoryUpdate()
│
├── If not running: set _isRunning = true, start CodeRepositoryUpdateLoopAsync
│
└── If already running: set _rerunRequested = true, return immediately

CodeRepositoryUpdateLoopAsync()
│
├── Run CodeRepositoryUpdateAsync() (processes ALL connectors)
│
└── Check _rerunRequested:
    ├── true: clear flag, loop again
    └── false: set _isRunning = false, exit
```

### TerminalSessionManager: Guid Overloads

Added to `TerminalSessionManager` to support callers providing their own session keys:

```csharp
// Get or create session with explicit Guid
Task<TerminalSession> GetOrCreateSessionAsync(Guid sessionId, CancellationToken ct)

// Execute command in a specific session
Task<(string output, int exitCode)> ExecuteCommandAsync(Guid sessionId, string command, CancellationToken ct)

// Dispose a specific session (for cleanup)
void DisposeSession(Guid sessionId)
```

The existing ThreadContextAccessor-based `GetOrCreateSessionAsync()` now delegates to the Guid overload. Fully backward-compatible.

### Parallel Processing

Each batch uses `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 3`. Each parallel slot gets its own `Guid` session key, creating an isolated terminal session. Sessions are cleaned up after each connector via `DisposeSession()`.

---

## Files Modified

| File | Change |
|------|--------|
| `Agent.Plugins/Services/TerminalSessionManager.cs` | Added Guid overloads + DisposeSession |
| `Agent.Web/Services/TsgConnectorCloneService.cs` | Full rewrite |
| `Agent.Web/Controllers/v1/TsgConnectorController.cs` | `RequestClone(name)` → `QueueCodeRepositoryUpdate()` |

## What Was Removed

- `Channel<CloneRequest>` and all channel infrastructure
- `ConcurrentDictionary<string, bool> _inProgressClones`
- `CloneRequest` record
- `GitOperationResult` record
- `RequestClone()` method
- `QueueStartupSyncAsync()` (replaced by `QueueCodeRepositoryUpdate()` in `ExecuteAsync`)
- `PeriodicSyncAsync()` (replaced by `PeriodicTimer` in `ExecuteAsync`)
- All direct `Process.Start` git code (replaced by `TerminalSessionManager` calls)
- `IServiceProvider` dependency (direct injection of singletons instead)
- `ExecuteGitCommandAsync`, `ExecuteGitCloneAsync`, `ExecuteGitPullAsync`

## What Was Kept

- `BuildAuthenticatedUrl` - builds PAT-authenticated URLs
- `ParseAzureDevOpsUrl` - parses dev.azure.com / visualstudio.com URLs
- `SanitizeFolderName` - sanitizes connector names for filesystem use
- `RedactPat` - redacts PAT tokens from error messages

---

## Git Credential Storage

After a successful clone/pull, the service sets up git `credential-store` with a per-repo credentials file inside `.git/`:

1. Configures `credential.helper` to `store --file .git/git-credentials`
2. Enables `credential.useHttpPath true`
3. Writes credential entry: `https://pat:{PAT}@{host}{path}`
4. Sets remote URL to clean URL (removes embedded PAT)

**Result:**
- `.git/config` has clean remote URL (no PAT visible)
- `.git/git-credentials` has the PAT (inside `.git/`, not tracked by git)
- LLM `git push/pull/fetch` from workspace terminal auto-uses stored credential
- Different repos can have different PATs

---

## State Transitions

```
CloneStatus per connector:

NotStarted ──→ Cloning ──→ Ready
                        └──→ Failed

Ready ──→ Syncing ──→ Ready
                  └──→ Failed

Failed ──→ Cloning/Syncing ──→ Ready/Failed

Stuck (Cloning/Syncing after crash) → treated as "needs clone" on next batch

On auth failure (PAT expired):
  - CloneStatus → Failed (with error message)
  - Status → Error (with "PAT may be expired" message)
  - Connector won't be picked up again until Status is set back to "Healthy"
    (requires user to update PAT via controller, which re-tests connectivity)
```

---

## Configuration

| Setting | Value | Rationale |
|---------|-------|-----------|
| MaxParallelClones | 3 | Balance between throughput and resource usage |
| StaleThreshold | 1 day | Daily sync is sufficient for docs/TSGs |
| MaintenanceInterval | 1 hour | Check for stale repos hourly |
| StartupDelay | 10 seconds | Let DI container fully initialize |

---

## DI Registration

No changes needed in Program.cs. `ITsgConnectorRepository` and `TerminalSessionManager` are both registered as singletons and are resolved automatically:

```csharp
builder.Services.AddSingleton<TsgConnectorCloneService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TsgConnectorCloneService>());
```

---

## Verification

1. **Build**: `dotnet build src/Agent/Agent.Web` - compiles clean
2. **Manual test**: Create a connector via API, verify clone and `CloneStatus` → `Ready`
3. **Credential test**: After clone, open workspace terminal, run `git -C <path> fetch` - should work without PAT prompt
4. **Debounce test**: Call `QueueCodeRepositoryUpdate()` multiple times rapidly - runs once with one rerun
5. **Parallelism**: Create 5+ connectors, verify 3 clone simultaneously
6. **Auth failure**: Use expired PAT, verify `Status` → `Error` and `CloneStatus` → `Failed`
7. **Startup**: Restart service, verify stale/never-indexed connectors get picked up
