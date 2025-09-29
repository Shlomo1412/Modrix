# Build and Release Process

This repository is configured with an automated build and release process that creates standalone executables for every commit and Windows installers for releases.

## Workflow Overview

The GitHub Actions workflows (`.github/workflows/`) perform the following steps:

### Regular Builds (`.github/workflows/dotnet-desktop.yml`)
Triggered on every push and pull request to the master branch:

1. **Build and Verify**: 
   - Builds the application in both Debug and Release configurations
   - Verifies build output and dependencies

2. **Publish Standalone Executable**:
   - Creates a self-contained executable in the `exe/` directory
   - Includes all .NET runtime dependencies
   - Optimized for Windows x64 architecture

3. **Upload Artifacts**:
   - Standalone executable (zip)

### Release Builds (`.github/workflows/release.yml`)
Triggered when a release is published:

1. **Build and Verify**: 
   - Builds the application in Release configuration
   - Verifies build output and dependencies

2. **Publish Standalone Executable**:
   - Creates a self-contained executable in the `exe/` directory
   - Includes all .NET runtime dependencies
   - Optimized for Windows x64 architecture

3. **Create Installer**:
   - Downloads and installs Inno Setup
   - Uses `Modrix.iss` configuration to create a Windows installer
   - Outputs installer to `installer/ModrixSetup.exe`

4. **Upload Release Assets**:
   - Adds installer and standalone executable to the GitHub release
   - Also uploads as workflow artifacts for debugging

## File Structure

- `.github/workflows/dotnet-desktop.yml` - CI/CD workflow for regular builds
- `.github/workflows/release.yml` - Release workflow with installer creation
- `Modrix.iss` - Inno Setup configuration for creating the installer (used only in releases)
- `exe/` - Generated standalone executable (ignored by git)
- `installer/` - Generated installer files (ignored by git, only created during releases)

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
```

To create installer manually (requires Inno Setup installed):
```bash
iscc Modrix.iss
```

## Requirements

- .NET 8.0 SDK or later
- Windows environment (for WPF and installer creation)
- Inno Setup 6.x (automatically installed in release workflow)

## Artifacts

### Regular Builds
Each successful build produces:
1. `Modrix-Standalone-Executable.zip` - Standalone executable and dependencies

### Release Builds
Each release produces:
1. `ModrixSetup.exe` - Inno Setup Windows installer (attached to release)
2. `Modrix.exe` - Standalone executable (attached to release)
3. `Modrix-Release-Installer.zip` - Installer as workflow artifact
4. `Modrix-Release-Executable.zip` - Executable as workflow artifact