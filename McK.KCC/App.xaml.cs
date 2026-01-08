using System.Windows;

namespace McK.KCC
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Check if this is a permission-check-only invocation (helper process)
            if (PermissionChecker.IsPermissionCheckOnly())
            {
                PermissionChecker.RunPermissionCheckAndExit();
                return;
            }

            // Check if the app was launched with elevated permissions or different user credentials
            // In these cases, we skip the preloading stages and go directly to MainWindow
            // because the current process already has the necessary permissions
            if (PermissionChecker.IsRunningWithGrantedPermissions())
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                MainWindow = mainWindow;
                return;
            }

            // Prevent automatic shutdown when the preloading window closes
            // This ensures we can show the main window after the modal dialog closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Show the preloading window to check permissions
            var preloadingWindow = new PreloadingWindow();
            preloadingWindow.ShowDialog();

            // If permission was granted via Stage 1 (current user already has permissions),
            // show the main window. For Stage 2/3, the app will restart with proper credentials.
            if (preloadingWindow.PermissionGranted)
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();

                // Now that the main window is shown, allow normal shutdown behavior
                // when the main window is closed
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                MainWindow = mainWindow;
            }
            else
            {
                // If permission wasn't granted and we haven't already shutdown, do so now
                // (This handles cases where the user manually closes the preloading window)
                Shutdown();
            }
        }
    }
}
