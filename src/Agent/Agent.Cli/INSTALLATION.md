# SREAgent CLI Installation Guide

## Option 1: Install as .NET Global Tool (Recommended)

### Step 1: Build and Pack the Tool
```bash
# Navigate to the CLI project directory
cd src/Agent/Agent.Cli

# Build the project
dotnet build --configuration Release

# Pack as a NuGet package
dotnet pack --configuration Release --output ./nupkg
```

### Step 2: Install as Global Tool
```bash
# Install from local package
dotnet tool install --global --add-source ./nupkg SREAgent.CLI

# Or install from a specific package file
dotnet tool install --global --add-source ./nupkg SREAgent.CLI --version 1.0.0
```

### Step 3: Use the Tool
```bash
# Now you can use srectl from anywhere
srectl agent create --name my_agent --instructions "My agent instructions" --tools Tool1 Tool2
srectl agent validate --name my_agent
srectl agent validate --all
srectl tools create --name MyTool --type KustoQuery
```

### Step 4: Update the Tool (when you make changes)
```bash
# Uninstall the old version
dotnet tool uninstall --global SREAgent.CLI

# Rebuild and pack
cd src/Agent/Agent.Cli
dotnet pack --configuration Release --output ./nupkg

# Reinstall
dotnet tool install --global --add-source ./nupkg SREAgent.CLI
```

## Option 2: PowerShell Function (Windows)

Add this function to your PowerShell profile:

### Step 1: Find your PowerShell profile location
```powershell
$PROFILE
```

### Step 2: Add the function to your profile
```powershell
function srectl {
    param(
        [Parameter(ValueFromRemainingArguments=$true)]
        [string[]]$Arguments
    )
    
    $cliPath = "C:\Users\ajsharm\source\repos\sreagent-runtime\src\Agent\Agent.Cli"
    Push-Location $cliPath
    try {
        dotnet run -- $Arguments
    }
    finally {
        Pop-Location
    }
}
```

### Step 3: Reload your profile or restart PowerShell
```powershell
. $PROFILE
```

## Option 3: Batch File (Windows)

### Step 1: Create srectl.bat
Create a file named `srectl.bat` in a directory that's in your PATH:

```batch
@echo off
pushd "C:\Users\ajsharm\source\repos\sreagent-runtime\src\Agent\Agent.Cli"
dotnet run -- %*
popd
```

### Step 2: Add to PATH
Place the batch file in a directory that's already in your PATH, or add a new directory to PATH.

## Option 4: Bash Alias (Linux/macOS/WSL)

Add this to your `~/.bashrc`, `~/.zshrc`, or equivalent:

```bash
alias srectl='cd /path/to/sreagent-runtime/src/Agent/Agent.Cli && dotnet run --'
```

Or create a more robust function:

```bash
srectl() {
    local current_dir=$(pwd)
    cd "/path/to/sreagent-runtime/src/Agent/Agent.Cli"
    dotnet run -- "$@"
    cd "$current_dir"
}
```

## Option 5: Pre-compiled Binary with Alias

### Step 1: Publish as self-contained
```bash
cd src/Agent/Agent.Cli

# Windows
dotnet publish -c Release -r win-x64 --self-contained -o ./publish/win-x64

# Linux
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish/linux-x64

# macOS
dotnet publish -c Release -r osx-x64 --self-contained -o ./publish/osx-x64
```

### Step 2: Copy to a directory in PATH
```bash
# Windows (copy to a directory in PATH)
copy .\publish\win-x64\Agent.Cli.exe "C:\Users\[Username]\AppData\Local\Microsoft\WindowsApps\srectl.exe"

# Linux/macOS (copy to /usr/local/bin or ~/bin)
sudo cp ./publish/linux-x64/Agent.Cli /usr/local/bin/srectl
chmod +x /usr/local/bin/srectl
```

## Recommended Approach

**For Development**: Use Option 1 (.NET Global Tool) as it's the most professional and works across platforms.

**For Quick Setup**: Use Option 2 (PowerShell Function) if you're on Windows and want something quick.

**For Distribution**: Use Option 1 or 5 depending on whether users have .NET installed.

## Usage Examples

Once you have `srectl` set up, you can use it like this:

```bash
# Create a new agent
srectl agent create --name incident_manager \
  --instructions "You help manage incidents and coordinate responses" \
  --tools PagerDutyTool SlackTool \
  --handoffs meta_agent

# Validate agents
srectl agent validate --name MyAgent
srectl agent validate --all
srectl agent validate --file agents/my_agent/my_agent.yaml

# Create tools
srectl tools create --name KustoHealthCheck --type KustoQuery --extra description "Health check query"

# Get help
srectl --help
srectl agent --help
srectl tools --help
```

## Troubleshooting

### Global Tool Issues
```bash
# List installed global tools
dotnet tool list --global

# Uninstall if needed
dotnet tool uninstall --global SREAgent.CLI

# Clear NuGet cache if having issues
dotnet nuget locals all --clear
```

### PATH Issues
Make sure the directory containing your alias/executable is in your system PATH environment variable.
