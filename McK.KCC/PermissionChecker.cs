using System;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

namespace McK.KCC
{
    /// <summary>
    /// Utility class for checking registry write permissions.
    /// </summary>
    public static class PermissionChecker
    {
        private const string TestKeyPath = @"SOFTWARE\McK.KCC.PermissionTest";
        private const string TestValueName = "PermissionTestValue";

        /// <summary>
        /// Checks if the current user has permission to write to the registry.
        /// </summary>
        /// <returns>True if registry write is successful, false otherwise.</returns>
        public static bool CanWriteToRegistry()
        {
            try
            {
                // Try to write to HKEY_LOCAL_MACHINE (System scope) 
                // This requires elevated permissions
                using (var key = Registry.LocalMachine.CreateSubKey(TestKeyPath, true))
                {
                    if (key == null)
                        return false;

                    // Write a test value
                    key.SetValue(TestValueName, DateTime.Now.Ticks.ToString());
                    
                    // Clean up - delete the test value and key
                    key.DeleteValue(TestValueName, false);
                }

                // Also try to delete the test key
                Registry.LocalMachine.DeleteSubKey(TestKeyPath, false);

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
            catch (Exception)
            {
                // Other exceptions (like IOException) also indicate failure
                return false;
            }
        }

        /// <summary>
        /// Checks if the current process is running with administrator privileges.
        /// </summary>
        /// <returns>True if running as administrator, false otherwise.</returns>
        public static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the current executable path safely.
        /// </summary>
        /// <returns>The executable path, or null if it cannot be determined.</returns>
        private static string? GetExecutablePath()
        {
            return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        }

        /// <summary>
        /// Restarts the application with elevated (administrator) privileges.
        /// </summary>
        /// <returns>True if the restart was initiated, false if it failed.</returns>
        public static bool RestartAsAdministrator()
        {
            try
            {
                var exePath = GetExecutablePath();
                
                if (string.IsNullOrEmpty(exePath))
                    return false;

                var processInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas", // This triggers UAC elevation
                    Arguments = "--elevated"
                };

                Process.Start(processInfo);
                return true;
            }
            catch (Exception)
            {
                // User cancelled UAC or other error
                return false;
            }
        }

        /// <summary>
        /// Restarts the application prompting for different user credentials.
        /// Uses Windows built-in runas functionality to prompt for credentials.
        /// </summary>
        /// <returns>True if the restart was initiated, false if it failed.</returns>
        public static bool RestartAsDifferentUser()
        {
            try
            {
                var exePath = GetExecutablePath();
                
                if (string.IsNullOrEmpty(exePath))
                    return false;

                // Use runas.exe directly without cmd.exe to avoid command injection
                // The /noprofile flag speeds up the process by not loading the user profile
                // The /user: flag without a specific user will prompt for credentials
                var processInfo = new ProcessStartInfo
                {
                    FileName = "runas.exe",
                    // Use /savecred to potentially use cached credentials, prompts if not available
                    // /env passes current environment variables
                    Arguments = $"/env /savecred /user:Administrator \"{exePath}\" --different-user",
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                Process.Start(processInfo);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the current permission stage based on command line arguments.
        /// </summary>
        /// <returns>The current permission stage (1, 2, or 3).</returns>
        public static int GetCurrentStage()
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg == "--different-user")
                    return 3;
                if (arg == "--elevated")
                    return 2;
            }
            return 1;
        }
    }
}
