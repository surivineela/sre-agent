# Building Standalone SRECTL Executables

This directory contains scripts to build self-contained SRECTL executables that don't require .NET to be installed on the target machine.

## Quick Build

To build standalone executables for all supported platforms:

```powershell
# From the Agent.Cli directory
.\scripts\build-standalone.ps1
```

This will create executables in `bin/standalone/` for:
- **Windows x64**: `win-x64/srectl.exe`
- **Linux x64**: `linux-x64/srectl`
- **macOS Intel**: `osx-x64/srectl`
- **macOS Apple Silicon**: `osx-arm64/srectl`

## Build Options

```powershell
# Build only for Windows
.\scripts\build-standalone.ps1 -Runtimes @("win-x64")

# Build in Debug configuration
.\scripts\build-standalone.ps1 -Configuration Debug

# Clean and rebuild
.\scripts\build-standalone.ps1 -Clean

# Custom output directory
.\scripts\build-standalone.ps1 -OutputPath "dist"
```

## Manual Build Command

If you prefer to build manually for a specific platform:

```powershell
# Windows x64
dotnet publish --no-restore -c Release -r win-x64 -o bin/standalone/win-x64 --self-contained true /p:PublishSingleFile=true

# Linux x64
dotnet publish --no-restore -c Release -r linux-x64 -o bin/standalone/linux-x64 --self-contained true /p:PublishSingleFile=true

# macOS Intel
dotnet publish --no-restore -c Release -r osx-x64 -o bin/standalone/osx-x64 --self-contained true /p:PublishSingleFile=true

# macOS Apple Silicon
dotnet publish --no-restore -c Release -r osx-arm64 -o bin/standalone/osx-arm64 --self-contained true /p:PublishSingleFile=true
```

## Executable Sizes

The self-contained executables are approximately 240-250 MB each because they include:
- The .NET runtime
- All required libraries
- The SRECTL application code
- Native dependencies

## Project Configuration

The following properties in `Agent.Cli.csproj` enable self-contained publishing:

```xml
<PropertyGroup>
  <!-- Self-contained publishing configuration -->
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <PublishTrimmed>false</PublishTrimmed>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <PublishReadyToRun>false</PublishReadyToRun>
</PropertyGroup>
```

## Distribution

The standalone executables can be distributed without any dependencies:

1. **Windows**: Just copy `srectl.exe` to the target machine
2. **Linux/macOS**: Copy `srectl`, make it executable with `chmod +x srectl`
3. No .NET installation required on the target machine
4. No package manager setup required

## Troubleshooting

### Trimming Issues

If you encounter issues with trimming (PublishTrimmed=true), this build is configured with `PublishTrimmed=false` to avoid reflection and serialization problems.

### File Not Found Errors

The code has been updated to use `AppContext.BaseDirectory` instead of `Assembly.Location` for single-file compatibility.

### Platform-Specific Issues

- **Windows**: The executable may be flagged by antivirus software initially
- **macOS**: You may need to allow the executable in Security & Privacy settings
- **Linux**: Ensure the executable has appropriate permissions (`chmod +x`)
