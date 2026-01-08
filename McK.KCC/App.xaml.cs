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

            // Prevent automatic shutdown when the preloading window closes
            // This ensures we can show the main window after the modal dialog closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Show the preloading window to check permissions
            var preloadingWindow = new PreloadingWindow();
            preloadingWindow.ShowDialog();

            // If permission was granted, show the main window
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
