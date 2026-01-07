# SRECTL Executables Package

This Universal Package contains self-contained SRECTL executables for multiple platforms.

## Package Information
- **Package Name**: srectl-executables
- **Version**: {{VERSION}}
- **Build Date**: {{BUILD_DATE}}
- **Platforms**: Windows (x64), Linux (x64), macOS (Intel x64), macOS (Apple Silicon ARM64)

## Files Included
- `srectl-win-x64.exe` - Windows executable
- `srectl-linux-x64` - Linux executable
- `srectl-osx-x64` - macOS Intel executable
- `srectl-osx-arm64` - macOS Apple Silicon executable
- `Templates/` - Agent templates and configuration files
- `Install.ps1` - Installation script (cross-platform)
- `Uninstall.ps1` - Uninstallation script (cross-platform)

## Quick Start

After downloading and extracting the package:

### Windows (PowerShell)
```powershell
.\Install.ps1
```

### Linux/macOS
```bash
pwsh Install.ps1
# or if PowerShell is not installed, see manual installation below
```

## Installation Locations

### Windows
```
C:\Users\<username>\AppData\Local\SRECTL\srectl.exe
C:\Users\<username>\AppData\Local\SRECTL\Templates\
```
The installation directory is added to your user PATH automatically.

### Linux/macOS
```
/usr/local/lib/srectl/srectl
/usr/local/lib/srectl/Templates/
/usr/local/bin/srectl (symlink)
```

## Manual Installation

If you cannot use PowerShell or prefer manual installation:

### Windows
1. Create directory: `C:\Users\<username>\AppData\Local\SRECTL\`
2. Copy `srectl-win-x64.exe` to the directory and rename to `srectl.exe`
3. Copy `Templates/` folder to the directory
4. Add `C:\Users\<username>\AppData\Local\SRECTL` to your PATH:
   - Open System Properties → Environment Variables
   - Edit user PATH variable
   - Add the directory path
   - Click OK and restart your terminal

### Linux
```bash
# Create installation directory
sudo mkdir -p /usr/local/lib/srectl

# Copy executable and templates
sudo cp srectl-linux-x64 /usr/local/lib/srectl/srectl
sudo cp -r Templates /usr/local/lib/srectl/
sudo chmod +x /usr/local/lib/srectl/srectl

# Create symlink
sudo ln -s /usr/local/lib/srectl/srectl /usr/local/bin/srectl

# Verify
srectl --version
```

### macOS
```bash
# Determine your architecture
uname -m  # arm64 = Apple Silicon, x86_64 = Intel

# Create installation directory
sudo mkdir -p /usr/local/lib/srectl

# Copy executable (choose appropriate version)
# For Apple Silicon:
sudo cp srectl-osx-arm64 /usr/local/lib/srectl/srectl
# For Intel:
sudo cp srectl-osx-x64 /usr/local/lib/srectl/srectl

# Copy templates
sudo cp -r Templates /usr/local/lib/srectl/
sudo chmod +x /usr/local/lib/srectl/srectl

# Create symlink
sudo ln -s /usr/local/lib/srectl/srectl /usr/local/bin/srectl

# Verify
srectl --version
```

## Verification

After installation:
```bash
srectl --version
srectl --help
```

## Getting Started

Initialize a new agent project:
```bash
srectl init --resource-url <your-server-url>
```

## Uninstallation

### Using Script
```powershell
.\Uninstall.ps1
```

### Manual Uninstallation

**Windows:**
1. Delete `C:\Users\<username>\AppData\Local\SRECTL`
2. Remove the path from your user PATH environment variable
3. Restart your terminal

**Linux/macOS:**
```bash
sudo rm -rf /usr/local/lib/srectl
sudo rm /usr/local/bin/srectl
```

## Support

For more information and documentation:
- GitHub: https://github.com/microsoft/sreagent-runtime
- Documentation: https://aka.ms/sreagent
