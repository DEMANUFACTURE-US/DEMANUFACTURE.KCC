using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace McK.KCC
{
    /// <summary>
    /// The "hang on, checking permissions" splash screen. Runs through three
    /// stages of permission checks trying to find some way to get registry access.
    /// Shows nice visual feedback so users know something is hapening.
    /// 
    /// Stage 1: Can current user write to registry? (unlikely on a normal PC)
    /// Stage 2: Can we get admin via UAC? (common path)
    /// Stage 3: Does the user know another account with rights? (last resort)
    /// </summary>
    public partial class PreloadingWindow : Window
    {
        // Tracks if any stage succeeded
        private bool _permissionGranted = false;
        
        // Set to true if user clicks cancel to abort the whole thing
        private bool _cancelled = false;

        /// <summary>
        /// Initializes the window and hooks up the Loaded event.
        /// We cant start async work in the constructor so we wait for Loaded.
        /// </summary>
        public PreloadingWindow()
        {
            InitializeComponent();
            Loaded += PreloadingWindow_Loaded;
        }

        /// <summary>
        /// Indicates wether permission was successfuly obtained via Stage 1.
        /// Stages 2 and 3 restart the app so they dont set this flag.
        /// </summary>
        public bool PermissionGranted => _permissionGranted;

        /// <summary>
        /// Starts the permission checking process once the window is loaded.
        /// Async void is normally bad but its fine for event handlers.
        /// </summary>
        private async void PreloadingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await RunAllPermissionChecksAsync();
        }

        /// <summary>
        /// Orchestrates running all three permission check stages in sequence.
        /// Stops as soon as one succeeds. If all fail, shows the denied window.
        /// </summary>
        private async Task RunAllPermissionChecksAsync()
        {
            // Brief delay so the UI can render before we start doing stuff
            await Task.Delay(500);

            if (_cancelled) return;

            // Try Stage 1: maybe were already running elevated somehow
            bool stage1Success = await RunStage1Async();
            if (stage1Success || _cancelled) return;

            // Try Stage 2: UAC elevation
            bool stage2Success = await RunStage2Async();
            if (stage2Success || _cancelled) return;

            // Last ditch: Stage 3 with alternate credentials
            await RunStage3Async();
        }

        /// <summary>
        /// Stage 1: Check if current user can write to HKLM.
        /// This is the fastest and simplest check. If it passes we skip
        /// all the other rigamarole and go straight to the main window.
        /// </summary>
        /// <returns>True if current user has perms, false otherwise</returns>
        private async Task<bool> RunStage1Async()
        {
            UpdateStatus("Checking current user permissions...", 
                "Testing if the current user can make changes to the Windows registry.");

            SetStageInProgress(1);
            
            // Give the user a second to see whats hapening
            await Task.Delay(1000);

            if (_cancelled) return false;

            // The actual permission test
            bool canWrite = PermissionChecker.CanWriteToRegistry();

            if (canWrite)
            {
                // Sweet, we can write! Mark success and proceed.
                SetStageSuccess(1);
                UpdateStatus("Permissions verified!", 
                    "Current user has registry write access. Loading application...",
                    isSuccess: true);

                await Task.Delay(1000);
                
                if (_cancelled) return false;
                
                _permissionGranted = true;
                Close();
                return true;
            }
            else
            {
                // Nope, need to try elevation
                SetStageFailed(1);
                Stage1Status.Text = "Insufficient permissions";
                UpdateStatus("Elevating privileges...", 
                    "Current user lacks permissions. Requesting administrator access...",
                    isWarning: true);

                await Task.Delay(1000);
                return false;
            }
        }

        /// <summary>
        /// Stage 2: Try to get admin rights via UAC.
        /// Spawns a helper process with elevation to test permissions.
        /// If it works, we restart the whole app as admin.
        /// </summary>
        /// <returns>True if elevation succeeded and app is restarting</returns>
        private async Task<bool> RunStage2Async()
        {
            UpdateStatus("Checking administrator permissions...", 
                "Testing if administrator account can make changes to the Windows registry.\nA UAC prompt may appear.");

            SetStageInProgress(2);
            
            await Task.Delay(500);

            if (_cancelled) return false;

            // This shows UAC prompt and waits for the helper process
            bool canWrite = PermissionChecker.CheckPermissionElevated();

            if (canWrite)
            {
                SetStageSuccess(2);
                UpdateStatus("Administrator permissions verified!", 
                    "Administrator has registry write access. Restarting with elevated permissions...",
                    isSuccess: true);

                await Task.Delay(1000);

                if (_cancelled) return false;

                // Actually restart the app with admin rights now
                var restartedProcess = PermissionChecker.RestartAsAdministrator();
                if (restartedProcess != null)
                {
                    // Make sure it didnt die immediately
                    await Task.Delay(500);
                    
                    if (!restartedProcess.HasExited)
                    {
                        // New process is running, we can exit
                        Application.Current.Shutdown();
                        return true;
                    }
                    else
                    {
                        // Process died, thats wierd but move to Stage 3
                        SetStageFailed(2);
                        Stage2Status.Text = "Restart failed";
                        UpdateStatus("Requesting different user credentials...", 
                            "Elevated process exited unexpectedly. Attempting to use different user credentials...",
                            isWarning: true);
                        await Task.Delay(1000);
                        return false;
                    }
                }
                else
                {
                    // Restart failed, continue to Stage 3
                    SetStageFailed(2);
                    Stage2Status.Text = "Restart failed";
                    UpdateStatus("Requesting different user credentials...", 
                        "Failed to restart with administrator privileges. Attempting to use different user credentials...",
                        isWarning: true);
                    await Task.Delay(1000);
                    return false;
                }
            }
            else
            {
                // UAC denied or admin doesnt have perms either (rare but possible)
                SetStageFailed(2);
                Stage2Status.Text = "Insufficient permissions";
                UpdateStatus("Requesting different user credentials...", 
                    "Administrator lacks permissions or elevation was cancelled. Attempting to use different user credentials...",
                    isWarning: true);

                await Task.Delay(1000);
                return false;
            }
        }

        /// <summary>
        /// Stage 3: Last resort, prompt for different user credentials.
        /// Maybe theres a service account or domain admin the user knows about.
        /// If this fails, we give up and show the permission denied window.
        /// </summary>
        private async Task RunStage3Async()
        {
            UpdateStatus("Checking different user permissions...", 
                "Please enter credentials for a user account with registry write access.");

            SetStageInProgress(3);
            
            await Task.Delay(500);

            if (_cancelled) return;

            // This shows a credential prompt and tests those creds
            bool canWrite = PermissionChecker.CheckPermissionAsDifferentUser();

            if (canWrite)
            {
                SetStageSuccess(3);
                UpdateStatus("Permissions verified!", 
                    "User has registry write access. Restarting with user credentials...",
                    isSuccess: true);

                await Task.Delay(1000);

                if (_cancelled) return;

                // Restart using the validated credentials we stored
                var restartedProcess = PermissionChecker.RestartWithValidatedCredentials();
                if (restartedProcess != null)
                {
                    await Task.Delay(500);
                    
                    if (!restartedProcess.HasExited)
                    {
                        // We're done here, new process takes over
                        Application.Current.Shutdown();
                        return;
                    }
                    else
                    {
                        // Something went wrong with the restart
                        SetStageFailed(3);
                        UpdateStatus("Permission check failed", 
                            "Process with user credentials exited unexpectedly.",
                            isError: true);

                        await Task.Delay(1500);

                        if (_cancelled) return;

                        ShowPermissionDeniedWindow();
                    }
                }
                else
                {
                    // Restart failed
                    SetStageFailed(3);
                    UpdateStatus("Permission check failed", 
                        "Failed to restart with user credentials.",
                        isError: true);

                    await Task.Delay(1500);

                    if (_cancelled) return;

                    ShowPermissionDeniedWindow();
                }
            }
            else
            {
                // Stage 3 failed too. Were out of options.
                SetStageFailed(3);
                UpdateStatus("Permission check failed", 
                    "None of the attempted user credentials have sufficient permissions to modify the Windows registry.",
                    isError: true);

                await Task.Delay(1500);

                if (_cancelled) return;

                // Show the bad news
                ShowPermissionDeniedWindow();
            }
        }

        /// <summary>
        /// Updates a stages indicator to show its currently running.
        /// Blue dot and "Checking..." text.
        /// </summary>
        /// <param name="stage">Which stage (1, 2, or 3)</param>
        private void SetStageInProgress(int stage)
        {
            var (icon, status) = GetStageControls(stage);
            if (icon != null && status != null)
            {
                icon.Text = "◉";
                icon.Foreground = (SolidColorBrush)FindResource("AccentBrush");
                status.Text = "Checking...";
                status.Foreground = (SolidColorBrush)FindResource("AccentBrush");
            }
        }

        /// <summary>
        /// Updates a stages indicator to show success.
        /// Green checkmark and "Permission granted" text.
        /// </summary>
        private void SetStageSuccess(int stage)
        {
            var (icon, status) = GetStageControls(stage);
            if (icon != null && status != null)
            {
                icon.Text = "✓";
                icon.Foreground = (SolidColorBrush)FindResource("SuccessBrush");
                status.Text = "Permission granted";
                status.Foreground = (SolidColorBrush)FindResource("SuccessBrush");
            }
        }

        /// <summary>
        /// Updates a stages indicator to show failure.
        /// Red X and "Failed" text. How depresing.
        /// </summary>
        private void SetStageFailed(int stage)
        {
            var (icon, status) = GetStageControls(stage);
            if (icon != null && status != null)
            {
                icon.Text = "✗";
                icon.Foreground = (SolidColorBrush)FindResource("ErrorBrush");
                status.Text = "Failed";
                status.Foreground = (SolidColorBrush)FindResource("ErrorBrush");
            }
        }

        /// <summary>
        /// Helper to get the UI controls for a given stage.
        /// Using a tuple here because C# is cool like that now.
        /// </summary>
        /// <returns>Tuple of (icon TextBlock, status TextBlock)</returns>
        private (System.Windows.Controls.TextBlock? icon, System.Windows.Controls.TextBlock? status) GetStageControls(int stage)
        {
            return stage switch
            {
                1 => (Stage1Icon, Stage1Status),
                2 => (Stage2Icon, Stage2Status),
                3 => (Stage3Icon, Stage3Status),
                _ => (null, null)  // Should never happen but makes the compiler happy
            };
        }

        /// <summary>
        /// Updates the status message box at the bottom of the window.
        /// Has different colors for different states: info, success, warning, error.
        /// </summary>
        /// <param name="title">Main status message</param>
        /// <param name="message">Secondary descriptive text</param>
        /// <param name="isSuccess">Green theme</param>
        /// <param name="isWarning">Yellow/orange theme</param>
        /// <param name="isError">Red theme</param>
        private void UpdateStatus(string title, string message, bool isSuccess = false, bool isWarning = false, bool isError = false)
        {
            StatusTitle.Text = title;
            StatusMessage.Text = message;

            // Determine colors and icon based on state
            SolidColorBrush brush;
            SolidColorBrush backgroundBrush;
            string icon;

            if (isSuccess)
            {
                brush = (SolidColorBrush)FindResource("SuccessBrush");
                backgroundBrush = (SolidColorBrush)FindResource("StatusSuccessBackgroundBrush");
                icon = "✓";
            }
            else if (isWarning)
            {
                brush = (SolidColorBrush)FindResource("WarningBrush");
                backgroundBrush = (SolidColorBrush)FindResource("StatusWarningBackgroundBrush");
                icon = "⚠";
            }
            else if (isError)
            {
                brush = (SolidColorBrush)FindResource("ErrorBrush");
                backgroundBrush = (SolidColorBrush)FindResource("StatusErrorBackgroundBrush");
                icon = "✗";
            }
            else
            {
                // Default info state (blue)
                brush = (SolidColorBrush)FindResource("AccentBrush");
                backgroundBrush = (SolidColorBrush)FindResource("StatusInfoBackgroundBrush");
                icon = "⏳";
            }

            StatusBorder.Background = backgroundBrush;
            StatusTitle.Foreground = brush;
            StatusIcon.Foreground = brush;
            StatusIcon.Text = icon;
        }

        /// <summary>
        /// Shows the permission denied window and closes this one.
        /// Has to be done via the Closed event to ensure proper cleanup.
        /// </summary>
        private void ShowPermissionDeniedWindow()
        {
            // Show the denied window AFTER this one closes
            Closed += (s, e) =>
            {
                var permissionDeniedWindow = new PermissionDeniedWindow();
                permissionDeniedWindow.Show();
            };
            Close();
        }

        /// <summary>
        /// Handles the cancel button. Sets the flag and shuts everything down.
        /// User decided they dont want to deal with permissions today.
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cancelled = true;
            Application.Current.Shutdown();
        }
    }
}
