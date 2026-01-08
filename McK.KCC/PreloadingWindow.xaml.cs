using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace McK.KCC
{
    /// <summary>
    /// Preloading window that checks registry permissions in 3 stages.
    /// All stages run sequentially within the same application instance.
    /// </summary>
    public partial class PreloadingWindow : Window
    {
        private bool _permissionGranted = false;
        private bool _cancelled = false;

        public PreloadingWindow()
        {
            InitializeComponent();
            Loaded += PreloadingWindow_Loaded;
        }

        /// <summary>
        /// Gets whether permission was successfully granted.
        /// </summary>
        public bool PermissionGranted => _permissionGranted;

        private async void PreloadingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await RunAllPermissionChecksAsync();
        }

        private async Task RunAllPermissionChecksAsync()
        {
            // Small delay to let UI render
            await Task.Delay(500);

            if (_cancelled) return;

            // Run all stages sequentially - stop when one succeeds
            
            // Stage 1: Check current user permissions
            bool stage1Success = await RunStage1Async();
            if (stage1Success || _cancelled) return;

            // Stage 2: Check with administrator elevation
            bool stage2Success = await RunStage2Async();
            if (stage2Success || _cancelled) return;

            // Stage 3: Check with different user credentials
            await RunStage3Async();
        }

        private async Task<bool> RunStage1Async()
        {
            UpdateStatus("Checking current user permissions...", 
                "Testing if the current user can make changes to the Windows registry.");

            SetStageInProgress(1);
            
            await Task.Delay(1000); // Give user time to see the status

            if (_cancelled) return false;

            bool canWrite = PermissionChecker.CanWriteToRegistry();

            if (canWrite)
            {
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
                SetStageFailed(1);
                Stage1Status.Text = "Insufficient permissions";
                UpdateStatus("Elevating privileges...", 
                    "Current user lacks permissions. Requesting administrator access...",
                    isWarning: true);

                await Task.Delay(1000);
                return false;
            }
        }

        private async Task<bool> RunStage2Async()
        {
            UpdateStatus("Checking administrator permissions...", 
                "Testing if administrator account can make changes to the Windows registry.\nA UAC prompt may appear.");

            SetStageInProgress(2);
            
            await Task.Delay(500);

            if (_cancelled) return false;

            // Use helper process to check permissions with elevation
            bool canWrite = PermissionChecker.CheckPermissionElevated();

            if (canWrite)
            {
                SetStageSuccess(2);
                UpdateStatus("Administrator permissions verified!", 
                    "Administrator has registry write access. Restarting with elevated permissions...",
                    isSuccess: true);

                await Task.Delay(1000);

                if (_cancelled) return false;

                // Restart the application with administrator elevation
                // The restarted app will have the --elevated flag and skip permission checks
                bool restarted = PermissionChecker.RestartAsAdministrator();
                if (restarted)
                {
                    // Don't set _permissionGranted to true because the current process should exit
                    // The new elevated process will handle the MainWindow
                    Application.Current.Shutdown();
                    return true;
                }
                else
                {
                    // If restart failed, continue to Stage 3
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
                SetStageFailed(2);
                Stage2Status.Text = "Insufficient permissions";
                UpdateStatus("Requesting different user credentials...", 
                    "Administrator lacks permissions or elevation was cancelled. Attempting to use different user credentials...",
                    isWarning: true);

                await Task.Delay(1000);
                return false;
            }
        }

        private async Task RunStage3Async()
        {
            UpdateStatus("Checking different user permissions...", 
                "Please enter credentials for a user account with registry write access.");

            SetStageInProgress(3);
            
            await Task.Delay(500);

            if (_cancelled) return;

            // Use helper process to check permissions with different credentials
            // This will also store the validated credentials for reuse
            bool canWrite = PermissionChecker.CheckPermissionAsDifferentUser();

            if (canWrite)
            {
                SetStageSuccess(3);
                UpdateStatus("Permissions verified!", 
                    "User has registry write access. Restarting with user credentials...",
                    isSuccess: true);

                await Task.Delay(1000);

                if (_cancelled) return;

                // Restart the application with the validated user credentials
                // The restarted app will have the --different-user flag and skip permission checks
                bool restarted = PermissionChecker.RestartWithValidatedCredentials();
                if (restarted)
                {
                    // Don't set _permissionGranted to true because the current process should exit
                    // The new process will handle the MainWindow
                    Application.Current.Shutdown();
                }
                else
                {
                    // If restart failed, show error
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
                SetStageFailed(3);
                UpdateStatus("Permission check failed", 
                    "None of the attempted user credentials have sufficient permissions to modify the Windows registry.",
                    isError: true);

                await Task.Delay(1500);

                if (_cancelled) return;

                // Show the permission denied window and close this one
                ShowPermissionDeniedWindow();
            }
        }

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

        private (System.Windows.Controls.TextBlock? icon, System.Windows.Controls.TextBlock? status) GetStageControls(int stage)
        {
            return stage switch
            {
                1 => (Stage1Icon, Stage1Status),
                2 => (Stage2Icon, Stage2Status),
                3 => (Stage3Icon, Stage3Status),
                _ => (null, null)
            };
        }

        private void UpdateStatus(string title, string message, bool isSuccess = false, bool isWarning = false, bool isError = false)
        {
            StatusTitle.Text = title;
            StatusMessage.Text = message;

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
                brush = (SolidColorBrush)FindResource("AccentBrush");
                backgroundBrush = (SolidColorBrush)FindResource("StatusInfoBackgroundBrush");
                icon = "⏳";
            }

            StatusBorder.Background = backgroundBrush;
            StatusTitle.Foreground = brush;
            StatusIcon.Foreground = brush;
            StatusIcon.Text = icon;
        }

        private void ShowPermissionDeniedWindow()
        {
            // Close this window first, then show the permission denied window
            // Using Closed event to ensure proper window cleanup before showing the new one
            Closed += (s, e) =>
            {
                var permissionDeniedWindow = new PermissionDeniedWindow();
                permissionDeniedWindow.Show();
            };
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cancelled = true;
            Application.Current.Shutdown();
        }
    }
}
