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
    /// Utility class for checking registry write permissions through various methods.
    /// This bad boy handles three stages of permission escalation:
    ///   Stage 1: Check if current user can write
    ///   Stage 2: Try elevating to admin via UAC
    ///   Stage 3: Prompt for diffrent user credentials
    /// 
    /// Uses some Windows native APIs becuase .NET doesnt expose the cred dialog natively.
    /// P/Invoke is fun right? Right? Anyone?
    /// </summary>
    public static class PermissionChecker
    {
        // Path for our test registry key. We create, write, then delete this
        // to verify we actualy have write permissions
        private const string TestKeyPath = @"SOFTWARE\McK.KCC.PermissionTest";
        private const string TestValueName = "PermissionTestValue";

        #region CredUI Native Methods
        
        // Windows API buffer size constants. These are documented maxes.
        // If you try to exceed these, bad things happen. Dont ask how I know.
        private const int CREDUI_MAX_USERNAME_LENGTH = 513;
        private const int CREDUI_MAX_PASSWORD_LENGTH = 256;
        private const int CREDUI_MAX_DOMAIN_TARGET_LENGTH = 337;
        
        // CredUIPromptForCredentials flags - these control dialog behavior
        private const int CREDUI_FLAGS_GENERIC_CREDENTIALS = 0x40000;  // Generic creds, not domain specific
        private const int CREDUI_FLAGS_DO_NOT_PERSIST = 0x00002;        // Dont save creds to credential manager
        private const int CREDUI_FLAGS_ALWAYS_SHOW_UI = 0x00080;        // Force show even if creds cached
        private const int CREDUI_FLAGS_EXPECT_CONFIRMATION = 0x20000;   // We'll confirm them ourselves
        private const int CREDUI_FLAGS_EXCLUDE_CERTIFICATES = 0x00008;  // No smart card stuff
        
        // Win32 authentication error codes for handling bad credentials
        // These come from winerror.h and tell us exactly what went wrong
        private const int ERROR_LOGON_FAILURE = 1326;        // Wrong username or password
        private const int ERROR_ACCOUNT_RESTRICTION = 1327;  // Account policy restriction
        private const int ERROR_INVALID_LOGON_HOURS = 1328;  // Outside allowed hours
        private const int ERROR_INVALID_WORKSTATION = 1329;  // Cant log on from this PC
        private const int ERROR_PASSWORD_EXPIRED = 1330;     // Password needs changing
        private const int ERROR_ACCOUNT_DISABLED = 1331;     // Account is disabled

        /// <summary>
        /// Structure passed to the Windows credentials dialog.
        /// Has to be laid out exactly like Windows expects or boom.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDUI_INFO
        {
            public int cbSize;                         // Size of this structure
            public IntPtr hwndParent;                  // Parent window handle for modal behavior
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszMessageText;              // Message shown in dialog
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszCaptionText;              // Dialog title
            public IntPtr hbmBanner;                   // Custom banner bitmap (we dont use this)
        }

        /// <summary>
        /// P/Invoke declaration for the Windows credential dialog.
        /// This is the Unicode version, hence the W suffix.
        /// </summary>
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
        /// The main permission test. Tries to create, write to, and delete a test
        /// registry key in HKEY_LOCAL_MACHINE. If any of that fails, we dont have
        /// write permissions. Pretty straitforward but gets the job done.
        /// </summary>
        /// <returns>True if we can write to HKLM, false if anything goes wrong</returns>
        public static bool CanWriteToRegistry()
        {
            try
            {
                // Attempt to create a test key in HKLM\SOFTWARE
                // This is a system wide location that requires elevated perms
                using var key = Registry.LocalMachine.CreateSubKey(TestKeyPath, true);
                
                if (key == null)
                    return false;

                // Write a timestamped test value so we know the write actualy happened
                key.SetValue(TestValueName, DateTime.Now.Ticks.ToString());
                
                // Clean up after ourselves like good citizens
                key.DeleteValue(TestValueName, false);

                // Also nuke the whole test key
                Registry.LocalMachine.DeleteSubKey(TestKeyPath, false);

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // Most common failure. User simply doesnt have the rights.
                return false;
            }
            catch (System.Security.SecurityException)
            {
                // Another form of access denied. Thanks Windows for being consistent.
                return false;
            }
            catch (Exception)
            {
                // Catch all for other wierdness like IO errors
                return false;
            }
        }

        /// <summary>
        /// Checks if were running as an admin. Uses Windows built in role checking.
        /// Note: This is different from having registry write perms in all cases
        /// but usualy if your admin, you can write to HKLM.
        /// </summary>
        /// <returns>True if running with admin token, false otherwise</returns>
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
                // If we cant even check, assume the worst
                return false;
            }
        }

        /// <summary>
        /// Gets the path to our own executable. Needed for relaunching ourselves
        /// with different credentials. Has fallback in case ProcessPath is null.
        /// </summary>
        /// <returns>Full path to the EXE, or null if we somehow cant figure it out</returns>
        private static string? GetExecutablePath()
        {
            // Environment.ProcessPath is the modern way, MainModule is the backup
            return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        }

        /// <summary>
        /// Relaunches the application with admin privileges via UAC elevation.
        /// The "runas" verb triggers the familiar UAC prompt. User can accept or
        /// click No and we return null.
        /// </summary>
        /// <returns>The new Process if started, null if cancelled or failed</returns>
        public static Process? RestartAsAdministrator()
        {
            try
            {
                var exePath = GetExecutablePath();
                
                if (string.IsNullOrEmpty(exePath))
                    return null;

                var processInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,        // Required for runas verb
                    Verb = "runas",                // Magic word to trigger UAC
                    Arguments = "--elevated"       // So the new process knows how it was launched
                };

                return Process.Start(processInfo);
            }
            catch (Exception)
            {
                // User clicked No on UAC or something else went sideways
                return null;
            }
        }

        /// <summary>
        /// Prompts for alternate user credentials and launches the app as that user.
        /// This is Stage 3 of our permission escalation strategy. Uses the Windows
        /// CredUI API to show a proper credential dialog. If creds are wrong, it
        /// reprompts instead of just failing silently.
        /// 
        /// SECURITY NOTE: Password is converted to SecureString and the StringBuilder
        /// is cleared after use. Were doing our best here folks.
        /// </summary>
        /// <returns>True if we successfully launched with new creds, false otherwise</returns>
        public static bool RestartAsDifferentUser()
        {
            var exePath = GetExecutablePath();
            
            if (string.IsNullOrEmpty(exePath))
                return false;

            // Default to the current users domain for convienence
            string domain = Environment.UserDomainName;
            
            // Track auth errors so we can show appropriate messages on retry
            int lastAuthError = 0;
            
            // Loop until user cancels or provides valid creds
            while (true)
            {
                // Pre-fill domain\username to save typing
                var userNameBuilder = new StringBuilder(domain + @"\", CREDUI_MAX_USERNAME_LENGTH);
                var passwordBuilder = new StringBuilder(CREDUI_MAX_PASSWORD_LENGTH);
                
                // Set up the dialog appearance
                var credUI = new CREDUI_INFO
                {
                    cbSize = Marshal.SizeOf(typeof(CREDUI_INFO)),
                    hwndParent = IntPtr.Zero,
                    pszMessageText = lastAuthError != 0 
                        ? "The username or password is incorrect. Please try again."
                        : "Enter credentials for a user account with registry write permissions.",
                    pszCaptionText = "Keeper Configuration Creator - User Credentials",
                    hbmBanner = IntPtr.Zero
                };

                bool save = false; // We dont want Windows to remember these
                int flags = CREDUI_FLAGS_GENERIC_CREDENTIALS | 
                            CREDUI_FLAGS_DO_NOT_PERSIST | 
                            CREDUI_FLAGS_ALWAYS_SHOW_UI |
                            CREDUI_FLAGS_EXCLUDE_CERTIFICATES;
                
                // Show the credential dialog
                int result = CredUIPromptForCredentialsW(
                    ref credUI,
                    "KeeperConfigCreator",
                    IntPtr.Zero,
                    lastAuthError,
                    userNameBuilder,
                    CREDUI_MAX_USERNAME_LENGTH,
                    passwordBuilder,
                    CREDUI_MAX_PASSWORD_LENGTH,
                    ref save,
                    flags);
                
                // Non-zero means user cancelled or error occured
                if (result != 0)
                    return false;
                
                string userName = userNameBuilder.ToString();
                string password = passwordBuilder.ToString();
                
                // Parse the username which could be in DOMAIN\user or user@domain format
                string? userDomain = null;
                string userNameOnly = userName;
                
                if (userName.Contains('\\'))
                {
                    // Classic DOMAIN\username format
                    var parts = userName.Split('\\', 2);
                    userDomain = parts[0];
                    userNameOnly = parts[1];
                }
                else if (userName.Contains('@'))
                {
                    // UPN format: user@domain.com
                    var parts = userName.Split('@', 2);
                    userNameOnly = parts[0];
                    userDomain = parts[1];
                }
                else
                {
                    // Just a username, assume current domain
                    userDomain = domain;
                }

                // Convert to SecureString for slightly better security
                // Its not perfect but its better then a plain string sitting around
                var securePassword = new SecureString();
                foreach (char c in password)
                {
                    securePassword.AppendChar(c);
                }
                securePassword.MakeReadOnly();
                
                // SECURITY: Clear the password from the StringBuilder imediately
                passwordBuilder.Clear();

                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "--different-user",
                        UseShellExecute = false,       // Needed for credential passing
                        LoadUserProfile = true,        // Load the users profile/registry
                        UserName = userNameOnly,
                        Password = securePassword,
                        Domain = userDomain
                    };

                    var process = Process.Start(processInfo);
                    
                    if (process != null)
                    {
                        return true; // Success! New process launched.
                    }
                    
                    // Process.Start returned null which is wierd but possible
                    return false;
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    // Check if its a bad password situation that warrants retry
                    if (IsAuthenticationError(ex.NativeErrorCode))
                    {
                        lastAuthError = ex.NativeErrorCode;
                        continue; // Show dialog again with error message
                    }
                    
                    // Some other Win32 error, bail out
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
                finally
                {
                    // SECURITY: Always dispose the secure string
                    securePassword.Dispose();
                }
            }
        }

        /// <summary>
        /// Checks if a Win32 error code is related to authentication failure.
        /// These are the errors where it makes sense to reprompt the user
        /// for credentials rather then just giving up.
        /// </summary>
        /// <param name="errorCode">The Win32 error code from the exception</param>
        /// <returns>True if this is a credentials problem, false for other errors</returns>
        private static bool IsAuthenticationError(int errorCode)
        {
            return errorCode == ERROR_LOGON_FAILURE ||
                   errorCode == ERROR_ACCOUNT_RESTRICTION ||
                   errorCode == ERROR_INVALID_LOGON_HOURS ||
                   errorCode == ERROR_INVALID_WORKSTATION ||
                   errorCode == ERROR_PASSWORD_EXPIRED ||
                   errorCode == ERROR_ACCOUNT_DISABLED;
        }

        /// <summary>
        /// Figures out which permission stage were in based on command line args.
        /// Stage 1 = normal launch, Stage 2 = elevated, Stage 3 = different user
        /// </summary>
        /// <returns>1, 2, or 3 depending on how we were launched</returns>
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
            return 1; // Default is stage 1
        }

        /// <summary>
        /// Spawns an elevated helper process to check if admin CAN write to registry.
        /// This doesnt actually restart the app, just tests permissions. The helper
        /// exits with code 0 for success, 1 for failure. We wait up to 15 seconds
        /// because UAC prompts can take a while if the user is grabbing coffee.
        /// </summary>
        /// <returns>True if the elevated process could write to registry</returns>
        public static bool CheckPermissionElevated()
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
                    Verb = "runas",                      // Trigger UAC
                    Arguments = "--check-permission-only", // Special mode for permission test
                    CreateNoWindow = true                // Dont show a console window
                };

                var process = Process.Start(processInfo);
                if (process == null)
                    return false;

                // Wait up to 15 seconds for the check to complete
                process.WaitForExit(15000);
                
                // Exit code 0 means the permission check passed
                return process.ExitCode == 0;
            }
            catch (Exception)
            {
                // UAC cancelled or some other failure
                return false;
            }
        }

        /// <summary>
        /// Similar to CheckPermissionElevated but with user provided credentials.
        /// Shows a credential dialog, spawns a helper process as that user,
        /// and checks if THAT user can write to the registry. If the creds are
        /// valid but the user lacks perms, we still return false.
        /// 
        /// IMPORTENT: If successful, stores the credentials so we can reuse them
        /// when actualy restarting the app (to avoid prompting twice).
        /// </summary>
        /// <returns>True if the provided user can write to registry</returns>
        public static bool CheckPermissionAsDifferentUser()
        {
            var exePath = GetExecutablePath();
            
            if (string.IsNullOrEmpty(exePath))
                return false;

            string domain = Environment.UserDomainName;
            int lastAuthError = 0;
            
            // Keep prompting until user cancels or we get valid creds with perms
            while (true)
            {
                var userNameBuilder = new StringBuilder(domain + @"\", CREDUI_MAX_USERNAME_LENGTH);
                var passwordBuilder = new StringBuilder(CREDUI_MAX_PASSWORD_LENGTH);
                
                var credUI = new CREDUI_INFO
                {
                    cbSize = Marshal.SizeOf(typeof(CREDUI_INFO)),
                    hwndParent = IntPtr.Zero,
                    pszMessageText = lastAuthError != 0 
                        ? "The username or password is incorrect. Please try again."
                        : "Enter credentials for a user account with registry write permissions.",
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
                    lastAuthError,
                    userNameBuilder,
                    CREDUI_MAX_USERNAME_LENGTH,
                    passwordBuilder,
                    CREDUI_MAX_PASSWORD_LENGTH,
                    ref save,
                    flags);
                
                // User hit cancel
                if (result != 0)
                    return false;
                
                string userName = userNameBuilder.ToString();
                string password = passwordBuilder.ToString();
                
                // Parse username format
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
                    var parts = userName.Split('@', 2);
                    userNameOnly = parts[0];
                    userDomain = parts[1];
                }
                else
                {
                    userDomain = domain;
                }

                // Convert to SecureString for the ProcessStartInfo
                var securePassword = new SecureString();
                foreach (char c in password)
                {
                    securePassword.AppendChar(c);
                }
                securePassword.MakeReadOnly();
                
                // SECURITY: Clear plaintext password imediately
                passwordBuilder.Clear();

                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "--check-permission-only",
                        UseShellExecute = false,
                        LoadUserProfile = true,
                        UserName = userNameOnly,
                        Password = securePassword,
                        Domain = userDomain,
                        CreateNoWindow = true
                    };

                    var process = Process.Start(processInfo);
                    
                    if (process == null)
                        return false;

                    // Wait for the permission check helper
                    process.WaitForExit(15000);
                    
                    // Exit code 0 = success = this user can write to registry
                    if (process.ExitCode == 0)
                    {
                        // Store the creds for reuse when we actualy restart
                        // The null-forgiving operator is ok here cuz we know userDomain isnt null
                        StoreValidatedCredentials(userNameOnly, userDomain!, securePassword);
                        return true;
                    }
                    
                    // Creds were valid but user doesnt have registry perms
                    return false;
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    // Bad password? Reprompt.
                    if (IsAuthenticationError(ex.NativeErrorCode))
                    {
                        lastAuthError = ex.NativeErrorCode;
                        continue;
                    }
                    
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
                finally
                {
                    securePassword.Dispose();
                }
            }
        }

        /// <summary>
        /// Checks if this process was launched just to do a permission check.
        /// Helper processes get this argument and should just test perms then exit.
        /// </summary>
        /// <returns>True if were a permission check helper process</returns>
        public static bool IsPermissionCheckOnly()
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg == "--check-permission-only")
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Does the permission check and exits. Exit code 0 = can write, 1 = cannot.
        /// This is called when were running as a helper process.
        /// </summary>
        public static void RunPermissionCheckAndExit()
        {
            bool canWrite = CanWriteToRegistry();
            Environment.Exit(canWrite ? 0 : 1);
        }

        /// <summary>
        /// Checks if we were launched with permission flags, meaning the parent
        /// process already verified we have what we need. Skip the whole
        /// permission dance and go straight to the main window.
        /// </summary>
        /// <returns>True if launched with --elevated or --different-user</returns>
        public static bool IsRunningWithGrantedPermissions()
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg == "--elevated" || arg == "--different-user")
                    return true;
            }
            return false;
        }

        // Static storage for validated credentials from Stage 3
        // These are held in memory so we can reuse them when restarting
        // without prompting the user twice. Yes this is slightly sketchy
        // from a security perspective but the password IS in a SecureString.
        private static string? _validatedUserName;
        private static string? _validatedDomain;
        private static SecureString? _validatedPassword;

        /// <summary>
        /// Saves credentials after successful validation so we can reuse them
        /// when actualy restarting the app. Makes a copy of the SecureString
        /// since the original will be disposed.
        /// </summary>
        /// <param name="userName">Just the username part, no domain</param>
        /// <param name="domain">Domain or machine name</param>
        /// <param name="password">The SecureString password (will be copied)</param>
        internal static void StoreValidatedCredentials(string userName, string domain, SecureString password)
        {
            _validatedUserName = userName;
            _validatedDomain = domain;
            // Copy the SecureString since the caller will dispose theirs
            _validatedPassword = password.Copy();
            _validatedPassword.MakeReadOnly();
        }

        /// <summary>
        /// Clears stored credentials. Should be called when were done with them
        /// or on error cleanup. Properly disposes the SecureString.
        /// </summary>
        internal static void ClearValidatedCredentials()
        {
            _validatedUserName = null;
            _validatedDomain = null;
            _validatedPassword?.Dispose();
            _validatedPassword = null;
        }

        /// <summary>
        /// Restarts the app using the credentials we saved from the Stage 3 check.
        /// This way we dont have to prompt the user for credentials twice.
        /// </summary>
        /// <returns>The new process if successful, null if we didnt have creds saved</returns>
        public static Process? RestartWithValidatedCredentials()
        {
            // Make sure we actualy have credentials stored
            if (_validatedUserName == null || _validatedDomain == null || _validatedPassword == null)
                return null;

            var exePath = GetExecutablePath();
            if (string.IsNullOrEmpty(exePath))
                return null;

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--different-user",
                    UseShellExecute = false,
                    LoadUserProfile = true,
                    UserName = _validatedUserName,
                    Password = _validatedPassword,
                    Domain = _validatedDomain
                };

                return Process.Start(processInfo);
            }
            catch (Exception)
            {
                // Something went wrong with process launch
                return null;
            }
        }
    }
}
