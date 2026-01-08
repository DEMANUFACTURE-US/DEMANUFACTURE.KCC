using System.Windows;

namespace McK.KCC
{
    /// <summary>
    /// Window displayed when all permission check stages have failed.
    /// Informs the user to contact their system administrator.
    /// </summary>
    public partial class PermissionDeniedWindow : Window
    {
        public PermissionDeniedWindow()
        {
            InitializeComponent();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
            Application.Current.Shutdown();
        }
    }
}
