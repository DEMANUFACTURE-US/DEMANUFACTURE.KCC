using System.Windows;

namespace McK.KCC
{
    /// <summary>
    /// The main applicaiton entry point. Yes I spelled that wrong on purpose.
    /// This WPF app handles Keeper Security configuration settings because 
    /// aparently we needed yet another tool to manage enviroment variables.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Fires when the app starts up. Buckle up buttercup, were going on a 
        /// permission checking adventure. Three stages of fun!
        /// </summary>
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // First things first: are we a lowly helper process just checking perms?
            // If so, do the deed and get outta here. No need for fancy windows.
            if (PermissionChecker.IsPermissionCheckOnly())
            {
                PermissionChecker.RunPermissionCheckAndExit();
                return;
            }

            // Did someone already elevate us or run us as a diffrent user?
            // If yes, skip all the permission theater and go strait to the main show
            if (PermissionChecker.IsRunningWithGrantedPermissions())
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                MainWindow = mainWindow;
                return;
            }

            // This is the tricky part. We need explicit shutdown control because
            // we're about to show a modal dialog and WPF gets wierd if we dont
            // tell it exactly when to close. Trust me, I learned the hard way.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Time to check if we actualy have permission to do anything useful
            var preloadingWindow = new PreloadingWindow();
            preloadingWindow.ShowDialog();

            // If Stage 1 passed (user already has the goods), show the main window
            // Stage 2 and 3 restarts happen elsewhere so we wont get here for those
            if (preloadingWindow.PermissionGranted)
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();

                // OK now WPF can close normaly when the window closes
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                MainWindow = mainWindow;
            }
            else
            {
                // User bailed or permissions were denied. Pack it up, go home.
                Shutdown();
            }
        }
    }
}
