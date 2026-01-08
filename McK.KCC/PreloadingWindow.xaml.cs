using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace McK.KCC
{
    /// <summary>
    /// Preloading window that checks registry permissions in 3 stages.
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
            await RunPermissionChecksAsync();
        }

        private async Task RunPermissionChecksAsync()
        {
            int currentStage = PermissionChecker.GetCurrentStage();

            // Update UI to show which stage we're at
            UpdateStageVisuals(currentStage);

            // Small delay to let UI render
            await Task.Delay(500);

            if (_cancelled) return;

            switch (currentStage)
            {
                case 1:
                    await RunStage1Async();
                    break;
                case 2:
                    await RunStage2Async();
                    break;
                case 3:
                    await RunStage3Async();
                    break;
            }
        }

        private async Task RunStage1Async()
        {
            UpdateStatus("Checking current user permissions...", 
                "Testing if the current user can make changes to the Windows registry.");

            SetStageInProgress(1);
            
            await Task.Delay(1000); // Give user time to see the status

            if (_cancelled) return;

            bool canWrite = PermissionChecker.CanWriteToRegistry();

            if (canWrite)
            {
                SetStageSuccess(1);
                UpdateStatus("Permissions verified!", 
                    "Current user has registry write access. Loading application...",
                    isSuccess: true);

                await Task.Delay(1000);
                
                if (_cancelled) return;
                
                _permissionGranted = true;
                Close();
            }
            else
            {
                SetStageFailed(1);
                UpdateStatus("Elevating privileges...", 
                    "Current user lacks permissions. Restarting as Administrator...",
                    isWarning: true);

                await Task.Delay(1500);

                if (_cancelled) return;

                // Proceed to Stage 2 - restart as administrator
                bool restarted = PermissionChecker.RestartAsAdministrator();
                
                if (restarted)
                {
                    // Close this instance - the elevated instance will handle it
                    Application.Current.Shutdown();
                }
                else
                {
                    UpdateStatus("Elevation cancelled", 
                        "User cancelled administrator elevation. Application cannot continue without proper permissions.",
                        isError: true);
                }
            }
        }

        private async Task RunStage2Async()
        {
            // Mark Stage 1 as failed (since we're here, Stage 1 didn't work)
            SetStageFailed(1);
            Stage1Status.Text = "Insufficient permissions";

            UpdateStatus("Checking administrator permissions...", 
                "Testing if the administrator account can make changes to the Windows registry.");

            SetStageInProgress(2);
            
            await Task.Delay(1000);

            if (_cancelled) return;

            bool canWrite = PermissionChecker.CanWriteToRegistry();

            if (canWrite)
            {
                SetStageSuccess(2);
                UpdateStatus("Administrator permissions verified!", 
                    "Administrator has registry write access. Loading application...",
                    isSuccess: true);

                await Task.Delay(1000);

                if (_cancelled) return;

                _permissionGranted = true;
                Close();
            }
            else
            {
                SetStageFailed(2);
                UpdateStatus("Requesting different user credentials...", 
                    "Administrator lacks permissions. Attempting to run as a different user...",
                    isWarning: true);

                await Task.Delay(1500);

                if (_cancelled) return;

                // Proceed to Stage 3 - Run as different user
                bool restarted = PermissionChecker.RestartAsDifferentUser();
                
                if (restarted)
                {
                    Application.Current.Shutdown();
                }
                else
                {
                    UpdateStatus("Failed to restart", 
                        "Could not launch application as different user. Please manually run as a user with registry access.",
                        isError: true);
                }
            }
        }

        private async Task RunStage3Async()
        {
            // Mark Stages 1 and 2 as failed
            SetStageFailed(1);
            Stage1Status.Text = "Insufficient permissions";
            SetStageFailed(2);
            Stage2Status.Text = "Insufficient permissions";

            UpdateStatus("Checking different user permissions...", 
                "Testing if the provided user credentials have registry write access.");

            SetStageInProgress(3);
            
            await Task.Delay(1000);

            if (_cancelled) return;

            bool canWrite = PermissionChecker.CanWriteToRegistry();

            if (canWrite)
            {
                SetStageSuccess(3);
                UpdateStatus("Permissions verified!", 
                    "User has registry write access. Loading application...",
                    isSuccess: true);

                await Task.Delay(1000);

                if (_cancelled) return;

                _permissionGranted = true;
                Close();
            }
            else
            {
                SetStageFailed(3);
                UpdateStatus("Permission check failed", 
                    "None of the attempted user credentials have sufficient permissions to modify the Windows registry. Please contact your system administrator.",
                    isError: true);
            }
        }

        private void UpdateStageVisuals(int currentStage)
        {
            // All stages before current are marked based on their actual state
            // Stages at and after current are pending
            if (currentStage >= 2)
            {
                SetStageFailed(1);
                Stage1Status.Text = "Insufficient permissions";
            }

            if (currentStage >= 3)
            {
                SetStageFailed(2);
                Stage2Status.Text = "Insufficient permissions";
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
            string icon;

            if (isSuccess)
            {
                brush = (SolidColorBrush)FindResource("SuccessBrush");
                icon = "✓";
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x2F));
            }
            else if (isWarning)
            {
                brush = (SolidColorBrush)FindResource("WarningBrush");
                icon = "⚠";
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x35, 0x1E));
            }
            else if (isError)
            {
                brush = (SolidColorBrush)FindResource("ErrorBrush");
                icon = "✗";
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x26, 0x26));
            }
            else
            {
                brush = (SolidColorBrush)FindResource("AccentBrush");
                icon = "⏳";
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x2F));
            }

            StatusTitle.Foreground = brush;
            StatusIcon.Foreground = brush;
            StatusIcon.Text = icon;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cancelled = true;
            Application.Current.Shutdown();
        }
    }
}
