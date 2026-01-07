# McK.KCC - Keeper Configuration Creator

A Windows WPF application that creates the `KeeperConfig` environment variable for Keeper Security configuration.

## Overview

Keeper Configuration Creator is a simple utility application designed to set up the KeeperConfig environment variable on Windows machines. The application automatically runs with administrator privileges and sets the environment variable in both User and System scopes.

## Features

- **Automatic Administrator Elevation**: The application requires administrator privileges and will prompt for UAC elevation on startup
- **Dual Scope Configuration**: Sets the environment variable in both User and System scopes
- **Smart Detection**: Detects if the environment variable already exists and notifies whether it was created or overwritten
- **Error Handling**: Displays detailed error messages with a copy-to-clipboard feature for easy troubleshooting
- **Modern Dark UI**: Clean, modern interface with a dark theme
- **JSON Configuration**: Environment variable details are stored in a JSON file for easy extension

## Environment Variable

The application creates/updates the following environment variable:

| Property | Value |
|----------|-------|
| **Name** | `KeeperConfig` |
| **Scopes** | User, System |
| **Value** | Base64-encoded Keeper Security configuration |

## Requirements

- **Operating System**: Windows 10 or later (Windows 7/8/8.1 also supported)
- **Runtime**: .NET 8.0 or later
- **Privileges**: Administrator rights required

## Installation

1. Download the latest release
2. Extract the files to your desired location
3. Run `McK.KCC.exe`
4. When prompted by UAC, click "Yes" to allow administrator access

## Usage

1. Launch the application (UAC prompt will appear)
2. Review the environment variable details displayed
3. Click the "Apply KeeperConfig" button
4. The application will display the result:
   - **Created**: If the variable didn't exist before
   - **Updated/Overwritten**: If the variable already existed

## Building from Source

### Prerequisites
- Visual Studio 2022 or later
- .NET 8.0 SDK

### Build Steps

```bash
cd McK.KCC/McK.KCC
dotnet build
```

Or open `McK.KCC.sln` in Visual Studio and build the solution.

### Running

```bash
dotnet run
```

> **Note**: Running from command line still requires administrator privileges. Use an elevated command prompt or let the UAC prompt handle elevation.

## Configuration

The environment variable configuration is stored in `config.json`:

```json
{
  "variableName": "KeeperConfig",
  "variableValue": "base64-encoded-configuration-value"
}
```

You can modify this file to change the environment variable name or value before running the application.

## Error Handling

If an error occurs during the environment variable creation:

1. An error dialog will appear with details about the failure
2. Click "Copy" to copy the error details to clipboard
3. Click "Close" to dismiss the dialog

Common errors:
- **Access Denied**: Ensure you clicked "Yes" on the UAC prompt
- **Configuration Load Error**: Verify `config.json` exists and is valid JSON

## Project Structure

```
McK.KCC/
├── McK.KCC.sln          # Solution file
└── McK.KCC/
    ├── App.xaml                    # Application definition
    ├── App.xaml.cs                 # Application code-behind
    ├── MainWindow.xaml             # Main window UI
    ├── MainWindow.xaml.cs          # Main window logic
    ├── ErrorWindow.xaml            # Error dialog UI
    ├── ErrorWindow.xaml.cs         # Error dialog logic
    ├── McK.KCC.csproj    # Project file
    ├── app.manifest                # Application manifest (admin elevation)
    ├── config.json                 # Environment variable configuration
    ├── Resources/
    │   └── skull.ico               # Application icon
    └── Themes/
        └── DarkTheme.xaml          # Dark theme styles
```

## License

This project is part of the McK suite of tools.
