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
        /// Restarts the application with elevated (administrator) privileges.
        /// </summary>
        /// <returns>True if the restart was initiated, false if it failed.</returns>
        public static bool RestartAsAdministrator()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName,
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
        /// Uses Windows "Run as Different User" functionality.
        /// </summary>
        /// <returns>True if the restart was initiated, false if it failed.</returns>
        public static bool RestartAsDifferentUser()
        {
            try
            {
                var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                
                if (string.IsNullOrEmpty(exePath))
                    return false;

                // Use runas command with /user flag to prompt for different user credentials
                // This is equivalent to Shift+Right-Click > "Run as different user"
                var processInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c runas /user:DOMAIN\\Administrator \"{exePath} --different-user\"",
                    UseShellExecute = true
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
