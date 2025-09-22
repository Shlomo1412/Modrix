# Build and Release Process

This repository is configured with an automated build and release process that creates both standalone executables and Windows installers.

## Workflow Overview

The GitHub Actions workflow (`.github/workflows/dotnet-desktop.yml`) performs the following steps:

1. **Build and Verify**: 
   - Builds the application in both Debug and Release configurations
   - Verifies build output and dependencies

2. **Publish Standalone Executable**:
   - Creates a self-contained executable in the `exe/` directory
   - Includes all .NET runtime dependencies
   - Optimized for Windows x64 architecture

3. **Create Installers**:
   - Downloads and installs Inno Setup
   - Uses `Modrix.iss` configuration to create a Windows installer
   - Outputs installer to `installer/ModrixSetup.exe`
   - Downloads and installs NSIS (Nullsoft Scriptable Install System)
   - Uses `Modrix.nsi` configuration to create a custom GUI installer
   - Outputs NSIS installer to `nsis-installer/ModrixSetup-NSIS.exe`

4. **Upload Artifacts**:
   - Standalone executable (zip)
   - Inno Setup Windows installer (.exe)
   - NSIS Windows installer with custom GUI (.exe)

## File Structure

- `Modrix.iss` - Inno Setup configuration for creating the installer
- `Modrix.nsi` - NSIS configuration for creating the custom GUI installer
- `.github/workflows/dotnet-desktop.yml` - CI/CD workflow
- `exe/` - Generated standalone executable (ignored by git)
- `installer/` - Generated Inno Setup installer files (ignored by git)
- `nsis-installer/` - Generated NSIS installer files (ignored by git)

## Manual Build Process

To build locally:

```bash
# Restore dependencies
dotnet restore Modrix.sln

# Build Debug version
dotnet build Modrix.csproj --configuration Debug

# Build Release version  
dotnet build Modrix.csproj --configuration Release

# Create standalone executable
dotnet publish Modrix.csproj --configuration Release --runtime win-x64 --self-contained true --output exe

# Create installer (requires Inno Setup installed)
iscc Modrix.iss

# Create NSIS installer (requires NSIS installed)
makensis Modrix.nsi
```

## Requirements

- .NET 8.0 SDK or later
- Windows environment (for WPF and installer creation)
- Inno Setup 6.x (automatically installed in CI/CD)
- NSIS 3.x (automatically installed in CI/CD)

## Artifacts

Each successful build produces:
1. `Modrix-Standalone-Executable.zip` - Standalone executable and dependencies
2. `Modrix-Installer.zip` - Inno Setup Windows installer (ModrixSetup.exe)
3. `Modrix-NSIS-Installer.zip` - NSIS Windows installer with custom GUI (ModrixSetup-NSIS.exe)