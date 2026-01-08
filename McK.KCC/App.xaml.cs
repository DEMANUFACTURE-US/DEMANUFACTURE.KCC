using System.Windows;

namespace McK.KCC
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Show the preloading window to check permissions
            var preloadingWindow = new PreloadingWindow();
            preloadingWindow.ShowDialog();

            // If permission was granted, show the main window
            if (preloadingWindow.PermissionGranted)
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
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
