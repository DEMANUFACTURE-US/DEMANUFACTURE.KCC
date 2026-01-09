# McK.KCC - Keeper Configuration Creator

A Windows WPF application that configures Keeper Security settings by managing registry entries and IIS application settings. The application uses a sophisticated permission checking system to ensure it can write to the Windows registry before allowing configuration changes.

---

## Table of Contents

1. [Overview](#overview)
2. [What This Application Does](#what-this-application-does)
3. [How It Works - Beginning to End](#how-it-works---beginning-to-end)
   - [Application Startup Flow](#application-startup-flow)
   - [Permission Checking System](#permission-checking-system)
   - [Main Application Features](#main-application-features)
4. [The Three Permission Stages](#the-three-permission-stages)
5. [Main Window Functionality](#main-window-functionality)
   - [Local Registration Tab](#local-registration-tab)
   - [IIS Registration Tab](#iis-registration-tab)
6. [Configuration](#configuration)
7. [Requirements](#requirements)
8. [Installation](#installation)
9. [Building from Source](#building-from-source)
10. [Project Structure](#project-structure)
11. [Error Handling](#error-handling)
12. [Branding & Theming](#branding--theming)
13. [Technical Details](#technical-details)
14. [TODO](#todo)

---

## Overview

Keeper Configuration Creator (McK.KCC) is an internal utility application designed to simplify the deployment of Keeper Security configuration across Windows machines. The application sets a base64-encoded configuration value as both a Windows environment variable and as an IIS application setting, allowing applications on the machine to access Keeper Security credentials.

The application is built with .NET 8.0 and uses WPF (Windows Presentation Foundation) with a modern dark theme styled to McKenney's corporate branding.

---

## What This Application Does

The primary purpose of McK.KCC is to:

1. **Set Registry-Based Environment Variables**: Creates or updates the `KeeperConfig` environment variable in both User and System scopes of the Windows registry
2. **Configure IIS Web Applications**: Sets the `KeeperConfig` value in the `appSettings` section of IIS site configurations
3. **Ensure Proper Permissions**: Uses a multi-stage permission checking system to guarantee the application can write to protected registry locations before attempting any changes

The `KeeperConfig` value is a base64-encoded JSON string containing Keeper Security credentials including:
- Hostname
- Client ID
- Application owner public key
- Private key
- Application key

---

## How It Works - Beginning to End

### Application Startup Flow

When you launch `McK.KCC.exe`, the following sequence occurs:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Application Startup                          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│ Check: Is this a permission-check-only helper process?          │
│ (Launched with --check-permission-only flag)                    │
└─────────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              │ YES                           │ NO
              ▼                               ▼
┌─────────────────────────┐   ┌─────────────────────────────────┐
│ Run permission test     │   │ Check: Already running with     │
│ Exit with code 0 or 1   │   │ granted permissions?            │
│ (Success/Failure)       │   │ (--elevated or --different-user)│
└─────────────────────────┘   └─────────────────────────────────┘
                                              │
                              ┌───────────────┴───────────────┐
                              │ YES                           │ NO
                              ▼                               ▼
              ┌─────────────────────────┐   ┌─────────────────────┐
              │ Skip permission checks  │   │ Show Preloading     │
              │ Show Main Window        │   │ Window and begin    │
              │ directly                │   │ permission stages   │
              └─────────────────────────┘   └─────────────────────┘
```

### Permission Checking System

The application uses a three-stage permission escalation system to find a way to get registry write access:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Preloading Window                            │
│              "Checking Permissions" Screen                      │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│ STAGE 1: Current User Permissions                               │
│ - Attempts to create/write/delete a test registry key in HKLM  │
│ - Path: HKEY_LOCAL_MACHINE\SOFTWARE\McK.KCC.PermissionTest     │
│ - If successful: ✓ Proceed to Main Window                      │
│ - If failed: Continue to Stage 2                               │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼ (if Stage 1 fails)
┌─────────────────────────────────────────────────────────────────┐
│ STAGE 2: Administrator Permissions (UAC)                        │
│ - Spawns a helper process with "runas" verb (triggers UAC)     │
│ - Helper process runs with --check-permission-only flag        │
│ - Helper tests registry write access and exits with 0 or 1     │
│ - If helper succeeds: Restart main app as Administrator        │
│ - If failed/cancelled: Continue to Stage 3                     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼ (if Stage 2 fails)
┌─────────────────────────────────────────────────────────────────┐
│ STAGE 3: Different User Credentials                             │
│ - Shows Windows CredUI credential dialog                       │
│ - Pre-fills current domain in DOMAIN\username format           │
│ - Spawns helper process as that user to test permissions       │
│ - If valid credentials with write access: Restart as that user │
│ - If all attempts fail: Show Permission Denied Window          │
└─────────────────────────────────────────────────────────────────┘
```

### Main Application Features

Once permissions are verified, the Main Window provides two tabs:

1. **Local Registration Tab**: Manage registry-based environment variables
2. **IIS Registration Tab**: Manage IIS site application settings

---

## The Three Permission Stages

### Stage 1: Current User Permissions

**What happens:**
- The application attempts to create a test registry key at `HKEY_LOCAL_MACHINE\SOFTWARE\McK.KCC.PermissionTest`
- Writes a test value (current timestamp)
- Deletes the test value and key
- If all operations succeed, the user has sufficient permissions

**When it succeeds:**
- Typically when the application is already running as an administrator
- On some development machines where the user has local admin rights

**UI Feedback:**
- Blue "Checking..." indicator during the test
- Green checkmark "Permission granted" on success
- Red X "Insufficient permissions" on failure

### Stage 2: Administrator Permissions (UAC)

**What happens:**
- Uses Windows UAC (User Account Control) to request elevation
- Shows the familiar "Do you want to allow this app to make changes?" dialog
- If the user clicks "Yes", the app relaunches with admin privileges

**Technical details:**
- Uses `ProcessStartInfo` with `Verb = "runas"` to trigger UAC
- First spawns a helper process to test if elevation actually grants write access
- Only restarts the full app if the helper confirms permissions

**When it succeeds:**
- When the logged-in user is a local administrator
- When the user has UAC permission to elevate

**When it fails:**
- User clicks "No" on the UAC prompt
- The administrator account also lacks registry write permissions (rare)

### Stage 3: Different User Credentials

**What happens:**
- Shows a Windows credential dialog (using native CredUI API)
- User enters credentials for an account with sufficient permissions
- Application relaunches as that user

**Technical details:**
- Uses `CredUIPromptForCredentialsW` Windows API via P/Invoke
- Supports both `DOMAIN\username` and `user@domain.com` formats
- If wrong password is entered, shows error and reprompts
- Uses `SecureString` for password handling
- Credentials are stored temporarily to avoid prompting twice

**When to use:**
- When a service account with elevated permissions is available
- In corporate environments where regular users can't elevate
- When a different domain admin account is needed

---

## Main Window Functionality

### Local Registration Tab

This tab manages the `KeeperConfig` environment variable in the Windows registry.

**Registry Locations:**

| Scope | Registry Path |
|-------|---------------|
| User | `HKEY_CURRENT_USER\Environment\KeeperConfig` |
| System | `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment\KeeperConfig` |

**UI Elements:**

1. **User Scope Card**
   - Shows current status (Present/Not Present)
   - Green border = Present, Yellow/Orange border = Not Present
   - Button toggles between "UPDATE" (to add) and "REMOVE" (to delete)

2. **System Scope Card**
   - Same layout as User Scope
   - Requires admin privileges to modify

**How it works:**

1. On load, the app checks both registry locations for the `KeeperConfig` value
2. UI updates to show current state with color-coded indicators
3. Clicking the button prompts for confirmation
4. The registry operation is performed using 64-bit registry view
5. Success/error popup is shown
6. UI refreshes to reflect the new state

### IIS Registration Tab

This tab manages the `KeeperConfig` value in IIS web.config files.

**How it works:**

1. On load, the app uses `Microsoft.Web.Administration` to enumerate all IIS sites
2. For each site, checks if `appSettings` contains a `KeeperConfig` key
3. Sites are grouped and sorted:
   - Sites WITHOUT the setting (needs attention) - shown in yellow/orange
   - Sites WITH the setting (already configured) - shown in green
4. User selects a site from the dropdown
5. Button appears to UPDATE or REMOVE the setting
6. Changes are written to the site's web.config via IIS API

**If IIS is not installed:**
- The combo box and button are automatically disabled
- No error is shown (graceful degradation)

---

## Configuration

The application reads its configuration from `config.json` located in the same directory as the executable:

```json
{
  "variableName": "KeeperConfig",
  "variableValue": "base64-encoded-configuration-string"
}
```

**Fields:**

| Field | Description |
|-------|-------------|
| `variableName` | The name of the environment variable or appSetting key |
| `variableValue` | The base64-encoded Keeper Security configuration JSON |

**Modifying the configuration:**
1. Edit `config.json` with a text editor
2. Restart the application
3. The new value will be used for all subsequent operations

**If config.json is missing or invalid:**
- The app defaults to `variableName = "KeeperConfig"` with an empty value
- An error popup is shown but the app continues to function

---

## Requirements

| Requirement | Details |
|-------------|---------|
| **Operating System** | Windows 7, 8, 8.1, 10, or 11 |
| **Runtime** | .NET 8.0 Runtime (Windows Desktop) |
| **Privileges** | Administrator rights for System scope and IIS operations |
| **IIS** | Optional - IIS Registration tab requires IIS to be installed |

---

## Installation

### Standard Installation

1. Download the latest release ZIP file
2. Extract all files to a desired location (e.g., `C:\Tools\McK.KCC\`)
3. Ensure `config.json` is in the same folder as `McK.KCC.exe`
4. Run `McK.KCC.exe`

### Files Required

```
McK.KCC/
├── McK.KCC.exe                 # Main executable
├── McK.KCC.dll                 # Core library
├── McK.KCC.deps.json           # Dependency manifest
├── McK.KCC.runtimeconfig.json  # Runtime configuration
├── McK.KCC.pdb                 # Debug symbols (optional)
└── config.json                 # Configuration file (REQUIRED)
```

---

## Building from Source

### Prerequisites

- Visual Studio 2022 or later
- .NET 8.0 SDK
- Windows 10/11 development workload in Visual Studio

### Build Steps

**Using Visual Studio:**
1. Open `McK.KCC.sln`
2. Select Debug or Release configuration
3. Build → Build Solution (Ctrl+Shift+B)
4. Output will be in `McK.KCC/bin/[Debug|Release]/net8.0-windows/`

**Using Command Line:**
```bash
cd McK.KCC
dotnet build
```

**Creating a Release Build:**
```bash
cd McK.KCC
dotnet publish -c Release -r win-x64 --self-contained false
```

---

## Project Structure

```
McK.KCC/
├── McK.KCC.sln                         # Visual Studio solution file
├── README.md                           # This documentation
├── APP/                                # Pre-built application files
│   ├── McK.KCC.exe
│   ├── McK.KCC.dll
│   └── config.json
│
└── McK.KCC/                            # Source code project
    │
    ├── McK.KCC.csproj                  # Project file
    ├── app.manifest                    # Windows application manifest
    ├── config.json                     # Configuration template
    │
    ├── App.xaml                        # Application definition
    ├── App.xaml.cs                     # Application startup logic
    │
    ├── PreloadingWindow.xaml           # Permission checking UI
    ├── PreloadingWindow.xaml.cs        # Permission checking logic
    │
    ├── MainWindow.xaml                 # Main application UI
    ├── MainWindow.xaml.cs              # Main window logic
    │
    ├── PermissionChecker.cs            # Permission utility class
    │
    ├── ErrorWindow.xaml                # Error dialog UI
    ├── ErrorWindow.xaml.cs             # Error dialog logic
    │
    ├── ResultPopup.xaml                # Success dialog UI
    ├── ResultPopup.xaml.cs             # Success dialog logic
    │
    ├── PermissionDeniedWindow.xaml     # Permission denied UI
    ├── PermissionDeniedWindow.xaml.cs  # Permission denied logic
    │
    ├── Resources/
    │   ├── skull.ico                   # Application icon (legacy)
    │   ├── mck.png                     # McKenney's banner logo
    │   ├── mckcircle.png               # McKenney's circular icon
    │   └── colorsscheme.png            # Brand color palette reference
    │
    ├── Themes/
    │   └── DarkTheme.xaml              # Dark theme with McKenney's colors
    │
    └── Properties/
        └── PublishProfiles/            # Publish configurations
```

---

## Error Handling

### Error Dialog

When an error occurs, an `ErrorWindow` displays:
- Error title
- Detailed error message
- "Copy" button to copy error details to clipboard
- "Close" button to dismiss

### Common Errors

| Error | Cause | Solution |
|-------|-------|----------|
| "Access Denied" | No write permissions to registry | Complete all three permission stages |
| "Configuration Load Error" | Missing or invalid config.json | Verify config.json exists and is valid JSON |
| "Unable to open registry key" | Registry path doesn't exist | Should not normally occur |
| "IIS Error: Site not found" | Selected site was removed | Refresh the IIS sites list |

### Permission Denied Window

If all three permission stages fail, a `PermissionDeniedWindow` appears showing:
- All three stages failed
- Recommendation to contact System Administrator
- Close button exits the application

---

## Branding & Theming

The application uses McKenney's Inc. corporate branding:

### Color Palette

| Name | Hex | Usage |
|------|-----|-------|
| Background Dark | `#1A2A3A` | Main window background |
| Background Medium | `#243447` | Tab content area |
| Background Light | `#2E3E52` | Buttons, cards |
| Primary Blue | `#0066A1` | Accent colors, primary buttons |
| Accent Red | `#C31230` | Errors, warnings |
| Green | `#28A745` | Success states, "Update" buttons |
| Red | `#DC3545` | "Remove" buttons |
| Warning | `#F5A623` | Warning states |

### UI Components

- **Buttons**: Rounded corners (8px), hover states, three styles (Modern, Primary, Danger, Success)
- **Cards**: Dark background with rounded corners (12px)
- **TabControl**: Custom styled with rounded top corners
- **ComboBox**: Custom dropdown with dark theme

---

## Technical Details

### Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Web.Administration | 11.1.0 | IIS management API |

### Windows API Usage

The application uses P/Invoke to call native Windows APIs:

- `CredUIPromptForCredentialsW` - Shows the Windows credential dialog
- Flags used: Generic credentials, don't persist, always show UI, exclude certificates

### Registry Access

- Uses 64-bit registry view (`RegistryView.Registry64`) for consistency
- Test key path: `HKEY_LOCAL_MACHINE\SOFTWARE\McK.KCC.PermissionTest`
- User scope: `HKEY_CURRENT_USER\Environment`
- System scope: `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment`

### Command Line Arguments

| Argument | Purpose |
|----------|---------|
| `--check-permission-only` | Run as helper process for permission testing |
| `--elevated` | Indicates app was relaunched via UAC |
| `--different-user` | Indicates app was relaunched with different credentials |

---

## TODO

The following items are planned for future development:

- [ ] **UI Improvements**: The current UI leaves much to be desired and needs refinement
- [ ] **Run As Different User Dialog**: The credential prompt needs to default to the domain so that users don't need to type it in the username text box. Currently, the domain is pre-filled in `DOMAIN\` format, but the cursor starts in the password field, making it awkward if the user needs to modify the username
- [ ] **Cursor Focus**: The cursor should default to the username text box, not the password box, when the credential dialog appears
- [ ] **Sentry Integration**: Have Mike perform the Sentry integration for error tracking and monitoring
- [ ] **Third-Party Code Review**: The application needs a third-party code review for security and quality assurance

---

## License

This project is part of the McK suite of internal tools for McKenney's Inc.
