# SRECTL System Installation Guide

This guide explains how to install the SRECTL standalone executable system-wide with proper PATH configuration and aliases.

## Installation Methods

### Method 1: Automated PowerShell Script (Recommended)

The PowerShell script provides the most comprehensive installation with proper PATH management and alias creation.

#### For User-Only Installation (No Admin Required)
```powershell
# From the directory containing srectl.exe
.\scripts\install-executable.ps1 -UserInstall
```

#### For System-Wide Installation (Requires Admin)
```powershell
# Run PowerShell as Administrator, then:
.\scripts\install-executable.ps1
```

#### With Additional Options
```powershell
# User install with desktop shortcut
.\scripts\install-executable.ps1 -UserInstall -CreateDesktopShortcut

# Custom installation path
.\scripts\install-executable.ps1 -UserInstall -InstallPath "C:\MyTools\SRECTL"

# Force overwrite existing installation
.\scripts\install-executable.ps1 -UserInstall -Force
```

### Method 2: Simple Batch File (Windows Only)

For users who prefer a simpler approach without PowerShell:

```batch
# From the directory containing srectl.exe
.\scripts\install-executable.bat
```

This will:
- Install to `%LOCALAPPDATA%\SRECTL`
- Add to user PATH
- Create `sre.bat` alias
- Create uninstaller

### Method 3: Manual Installation

#### Windows Manual Steps:
```powershell
# 1. Create installation directory
$installDir = "$env:LOCALAPPDATA\SRECTL"
New-Item -ItemType Directory -Path $installDir -Force

# 2. Copy executable
Copy-Item "srectl.exe" "$installDir\srectl.exe"

# 3. Add to PATH
$currentPath = [Environment]::GetEnvironmentVariable("PATH", "User")
$newPath = "$currentPath;$installDir"
[Environment]::SetEnvironmentVariable("PATH", $newPath, "User")

# 4. Create batch alias
"@echo off`n`"$installDir\srectl.exe`" %*" | Out-File "$installDir\sre.bat" -Encoding ASCII

# 5. Restart terminal and test
# srectl --version
# sre --version
```

#### macOS/Linux Manual Steps:
```bash
# 1. Make executable
chmod +x srectl

# 2. Choose installation location
# System-wide (requires sudo):
sudo mv srectl /usr/local/bin/

# User-only:
mkdir -p ~/.local/bin
mv srectl ~/.local/bin/
echo 'export PATH="$PATH:$HOME/.local/bin"' >> ~/.bashrc

# 3. Create alias
echo 'alias sre="srectl"' >> ~/.bashrc

# 4. Reload shell and test
source ~/.bashrc
srectl --version
sre --version
```

## Installation Locations

### Windows
- **User Install**: `%LOCALAPPDATA%\SRECTL` (e.g., `C:\Users\username\AppData\Local\SRECTL`)
- **System Install**: `C:\Program Files\SRECTL`

### macOS/Linux
- **System Install**: `/usr/local/bin/srectl`
- **User Install**: `~/.local/bin/srectl`

## Aliases Created

After installation, you can use either:
- `srectl` - Full command name
- `sre` - Short alias

Both commands provide identical functionality.

## PATH Management

The installation scripts automatically:
1. Add the installation directory to your PATH
2. Update the appropriate scope (User or Machine)
3. Create the necessary environment variable entries

### Troubleshooting PATH Issues

If `srectl` is not found after installation:

1. **Restart your terminal** - PATH changes require a new session
2. **Check PATH manually**:
   ```powershell
   # Windows
   $env:PATH -split ";" | Select-String "SRECTL"
   
   # Linux/macOS
   echo $PATH | tr ':' '\n' | grep srectl
   ```
3. **Refresh environment** (Windows):
   ```powershell
   refreshenv  # If you have Chocolatey
   # Or restart terminal
   ```

## Verification

After installation, verify it works:

```bash
# Test main command
srectl --version
srectl --help

# Test alias
sre --version
sre --help

# Test specific functionality
srectl agent --help
sre init --help
```

## Uninstallation

### Automated Uninstall
Run the uninstaller created during installation:

```powershell
# Windows
C:\Users\username\AppData\Local\SRECTL\uninstall.ps1

# Or if installed system-wide
C:\Program Files\SRECTL\uninstall.ps1
```

### Manual Uninstall

#### Windows:
```powershell
# 1. Remove from PATH
$currentPath = [Environment]::GetEnvironmentVariable("PATH", "User")
$newPath = ($currentPath -split ";") | Where-Object { $_ -notlike "*SRECTL*" }
[Environment]::SetEnvironmentVariable("PATH", ($newPath -join ";"), "User")

# 2. Remove directory
Remove-Item "$env:LOCALAPPDATA\SRECTL" -Recurse -Force

# 3. Remove PowerShell alias (edit profile manually)
# notepad $PROFILE
```

#### macOS/Linux:
```bash
# 1. Remove executable
sudo rm /usr/local/bin/srectl
# or for user install: rm ~/.local/bin/srectl

# 2. Remove from shell config
# Edit ~/.bashrc or ~/.zshrc and remove the PATH and alias lines
```

## Integration with Development Environments

### VS Code
Add to VS Code's integrated terminal by ensuring SRECTL is in your PATH. The tool will be available in all terminal sessions.

### PowerShell ISE
The PowerShell alias (`sre`) will be available after reloading your profile or restarting PowerShell ISE.

### Windows Terminal
All aliases and PATH changes will be available in new terminal tabs.

## Security Considerations

### Windows Defender
The self-contained executable may be flagged by Windows Defender initially. This is normal for new executables. You can:
1. Allow the executable when prompted
2. Add the installation directory to Windows Defender exclusions

### macOS Gatekeeper
On macOS, you may need to:
1. Right-click the executable and select "Open"
2. Click "Open" in the security dialog
3. Or run: `xattr -d com.apple.quarantine srectl`

### Linux Permissions
Ensure the executable has proper permissions:
```bash
chmod +x srectl
```

## Advanced Configuration

### Custom Installation Paths
You can install to any directory by modifying the installation scripts or using the `-InstallPath` parameter:

```powershell
.\scripts\install-system-fixed.ps1 -UserInstall -InstallPath "D:\Tools\SRECTL"
```

### Multiple Versions
You can maintain multiple versions by installing to different directories:
```powershell
# Install v1.0.2
.\scripts\install-system-fixed.ps1 -UserInstall -InstallPath "$env:LOCALAPPDATA\SRECTL-1.0.2"

# Install v1.1.0 (when available)
.\scripts\install-system-fixed.ps1 -UserInstall -InstallPath "$env:LOCALAPPDATA\SRECTL-1.1.0"
```

Then create version-specific aliases or use full paths as needed.
