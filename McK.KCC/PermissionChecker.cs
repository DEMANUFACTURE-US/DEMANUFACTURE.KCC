using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
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

        #region CredUI Native Methods
        
        private const int CREDUI_MAX_USERNAME_LENGTH = 513;
        private const int CREDUI_MAX_PASSWORD_LENGTH = 256;
        private const int CREDUI_MAX_DOMAIN_TARGET_LENGTH = 337;
        
        // CredUIPromptForCredentials flags
        private const int CREDUI_FLAGS_GENERIC_CREDENTIALS = 0x40000;
        private const int CREDUI_FLAGS_DO_NOT_PERSIST = 0x00002;
        private const int CREDUI_FLAGS_ALWAYS_SHOW_UI = 0x00080;
        private const int CREDUI_FLAGS_EXPECT_CONFIRMATION = 0x20000;
        private const int CREDUI_FLAGS_EXCLUDE_CERTIFICATES = 0x00008;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDUI_INFO
        {
            public int cbSize;
            public IntPtr hwndParent;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszMessageText;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszCaptionText;
            public IntPtr hbmBanner;
        }

        [DllImport("credui.dll", CharSet = CharSet.Unicode)]
        private static extern int CredUIPromptForCredentialsW(
            ref CREDUI_INFO pUiInfo,
            string pszTargetName,
            IntPtr Reserved,
            int dwAuthError,
            StringBuilder pszUserName,
            int ulUserNameMaxChars,
            StringBuilder pszPassword,
            int ulPasswordMaxChars,
            ref bool pfSave,
            int dwFlags);

        #endregion

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
        /// Uses Windows CredUI API to show a standard credentials dialog that allows
        /// the user to enter username and password, defaulting to the current domain.
        /// </summary>
        /// <returns>True if the restart was initiated, false if it failed or cancelled.</returns>
        public static bool RestartAsDifferentUser()
        {
            try
            {
                var exePath = GetExecutablePath();
                
                if (string.IsNullOrEmpty(exePath))
                    return false;

                // Get the current domain for the default username
                string domain = Environment.UserDomainName;
                
                // Prepare username with domain prefix as default
                var userNameBuilder = new StringBuilder(domain + @"\", CREDUI_MAX_USERNAME_LENGTH);
                var passwordBuilder = new StringBuilder(CREDUI_MAX_PASSWORD_LENGTH);
                
                var credUI = new CREDUI_INFO
                {
                    cbSize = Marshal.SizeOf(typeof(CREDUI_INFO)),
                    hwndParent = IntPtr.Zero,
                    pszMessageText = "Enter credentials for a user account with registry write permissions.",
                    pszCaptionText = "Keeper Configuration Creator - User Credentials",
                    hbmBanner = IntPtr.Zero
                };

                bool save = false;
                int flags = CREDUI_FLAGS_GENERIC_CREDENTIALS | 
                            CREDUI_FLAGS_DO_NOT_PERSIST | 
                            CREDUI_FLAGS_ALWAYS_SHOW_UI |
                            CREDUI_FLAGS_EXCLUDE_CERTIFICATES;
                
                int result = CredUIPromptForCredentialsW(
                    ref credUI,
                    "KeeperConfigCreator",
                    IntPtr.Zero,
                    0,
                    userNameBuilder,
                    CREDUI_MAX_USERNAME_LENGTH,
                    passwordBuilder,
                    CREDUI_MAX_PASSWORD_LENGTH,
                    ref save,
                    flags);
                
                // User cancelled the dialog
                if (result != 0)
                    return false;
                
                string userName = userNameBuilder.ToString();
                string password = passwordBuilder.ToString();
                
                // Parse domain and username if provided in DOMAIN\user format
                string? userDomain = null;
                string userNameOnly = userName;
                
                if (userName.Contains('\\'))
                {
                    var parts = userName.Split('\\', 2);
                    userDomain = parts[0];
                    userNameOnly = parts[1];
                }
                else if (userName.Contains('@'))
                {
                    // Handle user@domain format
                    var parts = userName.Split('@', 2);
                    userNameOnly = parts[0];
                    userDomain = parts[1];
                }

                // Create a SecureString for the password
                var securePassword = new SecureString();
                foreach (char c in password)
                {
                    securePassword.AppendChar(c);
                }
                securePassword.MakeReadOnly();
                
                // Clear the password from memory
                passwordBuilder.Clear();

                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "--different-user",
                        UseShellExecute = false,
                        LoadUserProfile = true,
                        UserName = userNameOnly,
                        Password = securePassword,
                        Domain = userDomain ?? domain
                    };

                    Process.Start(processInfo);
                    return true;
                }
                finally
                {
                    securePassword.Dispose();
                }
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
