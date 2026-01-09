using System.Windows;

namespace McK.KCC
{
    /// <summary>
    /// The "we tried everything and nothing worked" window.
    /// This gets shown when all three permission check stages fail.
    /// Basicaly tells the user to contact IT because were out of options.
    /// Not the best user experience but sometimes you just gotta admit defeat.
    /// </summary>
    public partial class PermissionDeniedWindow : Window
    {
        /// <summary>
        /// Creates the permission denied window. Just initializes the component
        /// since theres no dynamic content here. The message is in the XAML.
        /// </summary>
        public PermissionDeniedWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles the close button click. Closes this window AND shuts down
        /// the entire application because theres nothing else we can do.
        /// Game over man, game over.
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
            Application.Current.Shutdown();
        }
    }
}
