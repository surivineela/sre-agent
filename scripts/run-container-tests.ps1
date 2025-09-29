<#!
.SYNOPSIS
Run E2E test container image and collect TRX (image + optional filter argument).

.DESCRIPTION
Invokes the test runner container whose ENTRYPOINT already supplies the test assembly.
You may optionally provide a filter expression which will be passed as a positional
argument to the container (not via environment variable). The container's internal
script will treat that argument as the xUnit filter, taking precedence over TEST_FILTER
environment variable if also set.

Defaults:
  - Results directory: ./container-test-results
  - Container name: e2e-tests-run
  - Container auto-removed (--rm)
  - Filter passed positionally (no env needed)

Usage (all tests):
  pwsh ./scripts/run-container-tests.ps1 <image>

Usage (filtered):
  pwsh ./scripts/run-container-tests.ps1 <image> "ClassName=SimpleNoOpTests"

Exit code of script == exit code of tests inside container.
TRX (if produced) will be under ./container-test-results
#>

param(
  [Parameter(Mandatory=$true, Position=0)] [string] $Image,
  [Parameter(Mandatory=$false, Position=1)] [string] $Filter
)

$ErrorActionPreference = 'Stop'

$ResultsDir = 'container-test-results'
$ContainerName = 'e2e-tests-run'
$EnableOidcRefresh = $false
if ($env:OIDC_REFRESH_ENABLED) {
  if ($env:OIDC_REFRESH_ENABLED -match '^(?i:true|1|yes)$') { $EnableOidcRefresh = $true }
}
$RefreshIntervalSeconds = 240 # 4 minutes
if ($env:OIDC_REFRESH_INTERVAL_SECONDS -as [int]) { $RefreshIntervalSeconds = [int]$env:OIDC_REFRESH_INTERVAL_SECONDS }

<#
OIDC Token Auto-Refresh
-----------------------
If AZURE_FEDERATED_TOKEN_FILE isn't supplied by the workflow already AND refresh is explicitly enabled
via OIDC_REFRESH_ENABLED=true|1|yes, we self-manage an Azure AD workload identity (GitHub OIDC) token
by repeatedly calling the GitHub OIDC endpoint exposed via ACTIONS_ID_TOKEN_REQUEST_URL / ACTIONS_ID_TOKEN_REQUEST_TOKEN.

Design:
 1. (Opt-in) Initial fetch performed synchronously prior to container start.
 2. Background job updates the token file every $RefreshIntervalSeconds (default 240) seconds.
 3. Token is written atomically via a temp file then Move-Item to avoid partial reads in container.
 4. On script exit we stop the background job to avoid orphan processes.
 5. Disabled by default; enable by setting OIDC_REFRESH_ENABLED=true.

Notes:
  - The token by default expires in 5 minutes; refreshing every 4 minutes keeps it valid.
  - The mounted path inside the container remains constant; only file content changes.
  - If the workflow already supplied AZURE_FEDERATED_TOKEN_FILE we reuse it as-is and DO NOT refresh.
#>

$ManagedOidcTokenFile = $null
$OidcRefreshJob = $null

function Get-GitHubOidcToken {
  param([string] $Audience = 'api://AzureADTokenExchange')
  if (-not $env:ACTIONS_ID_TOKEN_REQUEST_URL -or -not $env:ACTIONS_ID_TOKEN_REQUEST_TOKEN) {
    throw 'GitHub OIDC request environment variables not present; ensure id-token permission is enabled.'
  }
  $url = "$($env:ACTIONS_ID_TOKEN_REQUEST_URL)&audience=$Audience"
  try {
    $resp = Invoke-RestMethod -Headers @{ Authorization = "Bearer $($env:ACTIONS_ID_TOKEN_REQUEST_TOKEN)" } -Uri $url -Method Get -TimeoutSec 30
    if (-not $resp.value) { throw 'Response missing value field' }
    return $resp.value
  } catch {
    throw "Failed to acquire OIDC token: $($_.Exception.Message)"
  }
}

function Initialize-OidcManagedTokenFile {
  if ($env:AZURE_FEDERATED_TOKEN_FILE -and (Test-Path -LiteralPath $env:AZURE_FEDERATED_TOKEN_FILE)) {
    Write-Host 'Existing AZURE_FEDERATED_TOKEN_FILE detected; skipping self-managed refresh.' -ForegroundColor Cyan
    return $false
  }
  if (-not $EnableOidcRefresh) {
    Write-Host 'OIDC refresh not enabled (set OIDC_REFRESH_ENABLED=true to activate self-managed token).' -ForegroundColor Yellow
    return $false
  }
  try {
    $dir = Join-Path $env:RUNNER_TEMP 'oidc'
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $ManagedOidcTokenFile = Join-Path $dir 'gha-oidc-token'
    Write-Host "Initializing self-managed OIDC token file at $ManagedOidcTokenFile" -ForegroundColor Cyan
    $token = Get-GitHubOidcToken
    Set-Content -LiteralPath $ManagedOidcTokenFile -Value $token -NoNewline
    $script:ManagedOidcTokenFile = $ManagedOidcTokenFile
    # Export env var for downstream logic in this script
    $env:AZURE_FEDERATED_TOKEN_FILE = $ManagedOidcTokenFile
    return $true
  } catch {
    Write-Warning $_
    return $false
  }
}

function Start-OidcRefreshJob {
  if (-not $ManagedOidcTokenFile) { return }
  Write-Host "Starting background OIDC refresh job (interval ${RefreshIntervalSeconds}s)" -ForegroundColor Cyan
  $aud = 'api://AzureADTokenExchange'
  $job = Start-Job -ScriptBlock {
    param($File,$Interval,$Audience)
    $ErrorActionPreference='Stop'
    while ($true) {
      try {
        if (-not $env:ACTIONS_ID_TOKEN_REQUEST_URL -or -not $env:ACTIONS_ID_TOKEN_REQUEST_TOKEN) {
          Write-Warning 'OIDC env vars missing inside refresh job; exiting.'
          break
        }
        $url = "$($env:ACTIONS_ID_TOKEN_REQUEST_URL)&audience=$Audience"
        $resp = Invoke-RestMethod -Headers @{ Authorization = "Bearer $($env:ACTIONS_ID_TOKEN_REQUEST_TOKEN)" } -Uri $url -Method Get -TimeoutSec 30
        if ($resp.value) {
          $tmp = "$File.tmp"
          Set-Content -LiteralPath $tmp -Value $resp.value -NoNewline
          Move-Item -Force -Path $tmp -Destination $File
          Write-Host "[OIDC Refresh] Updated token $(Get-Date -Format o)" -ForegroundColor DarkCyan
        } else {
          Write-Warning '[OIDC Refresh] Response missing value; skipping update.'
        }
      } catch {
        Write-Warning "[OIDC Refresh] Failure: $($_.Exception.Message)"
      }
      Start-Sleep -Seconds $Interval
    }
  } -ArgumentList $ManagedOidcTokenFile,$RefreshIntervalSeconds,$aud
  $script:OidcRefreshJob = $job
}

function Stop-OidcRefreshJob {
  if ($OidcRefreshJob) {
    Write-Host 'Stopping OIDC refresh job...' -ForegroundColor Cyan
    try { Stop-Job -Job $OidcRefreshJob -Force -ErrorAction SilentlyContinue } catch { }
    try { Remove-Job -Job $OidcRefreshJob -Force -ErrorAction SilentlyContinue } catch { }
  }
}

Initialize-OidcManagedTokenFile | Out-Null
if ($ManagedOidcTokenFile) { Start-OidcRefreshJob }

Write-Host "== Running E2E tests in container ==" -ForegroundColor Cyan
Write-Host "Image: $Image" -ForegroundColor Cyan
if ($Filter) { Write-Host "Test filter: $Filter" -ForegroundColor Cyan }
Write-Host "Results directory: $ResultsDir" -ForegroundColor Cyan

# Prepare results directory
$fullResults = Resolve-Path (New-Item -ItemType Directory -Force -Path $ResultsDir) | Select-Object -ExpandProperty Path

# Build docker command (filter becomes positional argument)
$dockerArgs = @('run','--rm','--name', $ContainerName,'-v',"${fullResults}:/app/TestResults")

# OIDC workload identity support (preferred in CI). Use managed token file if present.
$hostTokenPath = $env:AZURE_FEDERATED_TOKEN_FILE
$containerTokenPath = '/var/run/secrets/azure/tokens/oidc-token'
if ($hostTokenPath -and (Test-Path -LiteralPath $hostTokenPath -PathType Leaf)) {
  Write-Host "OIDC detected. Mounting federated token file (auto-refresh: $([bool]$ManagedOidcTokenFile))." -ForegroundColor Cyan
  $dockerArgs += '-v'
  $dockerArgs += "${hostTokenPath}:${containerTokenPath}:ro"
  $dockerArgs += '-e'
  $dockerArgs += "AZURE_FEDERATED_TOKEN_FILE=$containerTokenPath"
  foreach ($var in 'AZURE_TENANT_ID','AZURE_CLIENT_ID','AZURE_SUBSCRIPTION_ID') {
    $val = (Get-Item -Path Env:$var -ErrorAction SilentlyContinue).Value
    if ($val) { $dockerArgs += '-e'; $dockerArgs += "$var=$val" }
  }
} else {
  Write-Host 'AZURE_FEDERATED_TOKEN_FILE not present or unreadable; OIDC not enabled for this run.' -ForegroundColor Yellow
}

# Always set a fixed test environment marker
$dockerArgs += '-e'
$dockerArgs += 'TEST_ENV=local-bicep'

# Propagate subscription id if available
if ($env:AZURE_SUBSCRIPTION_ID) {
  Write-Host "Propagating AZURE_SUBSCRIPTION_ID=$($env:AZURE_SUBSCRIPTION_ID) into container" -ForegroundColor Cyan
  $dockerArgs += '-e'
  $dockerArgs += "AZURE_SUBSCRIPTION_ID=$($env:AZURE_SUBSCRIPTION_ID)"
}

# Propagate Key Vault URI if available
if ($env:SREAGENT_TESTING_KEYVAULT_URI) {
  Write-Host "Propagating SREAGENT_TESTING_KEYVAULT_URI into container" -ForegroundColor Cyan
  $dockerArgs += '-e'
  $dockerArgs += "SREAGENT_TESTING_KEYVAULT_URI=$($env:SREAGENT_TESTING_KEYVAULT_URI)"
}

# Propagate fresh Agent image override if provided (allows tests to deploy/use the just-built PR image)
if ($env:SREAGENT_TESTING_IMAGE_OVERRIDE) {
  Write-Host "Propagating SREAGENT_TESTING_IMAGE_OVERRIDE=$($env:SREAGENT_TESTING_IMAGE_OVERRIDE) into container" -ForegroundColor Cyan
  $dockerArgs += '-e'
  $dockerArgs += "SREAGENT_TESTING_IMAGE_OVERRIDE=$($env:SREAGENT_TESTING_IMAGE_OVERRIDE)"
}
$dockerArgs += $Image
if ($Filter) { $dockerArgs += $Filter }

Write-Host "Executing: docker $($dockerArgs -join ' ')" -ForegroundColor Yellow

$exitCode = 0
& docker @dockerArgs || { $exitCode = $LASTEXITCODE }

# Stop refresh job after container exit
Stop-OidcRefreshJob

Write-Host "Container exit code: $exitCode" -ForegroundColor Cyan

# Locate TRX
$trx = Get-ChildItem -Path $fullResults -Filter *.trx -Recurse -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $trx) {
    Write-Warning "No TRX file found in $fullResults (container may have failed before test run)."
} else {
    Write-Host "TRX result: $($trx.FullName)" -ForegroundColor Green
}

exit $exitCode
