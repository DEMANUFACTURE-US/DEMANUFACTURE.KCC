using System;
using System.Windows;

namespace McK.KCC
{
    public partial class ErrorWindow : Window
    {
        private readonly string _errorDetails;

        public ErrorWindow(string title, string message)
        {
            InitializeComponent();
            
            ErrorTitle.Text = title;
            ErrorMessage.Text = message;
            _errorDetails = $"{title}\n\n{message}";
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_errorDetails);
                MessageBox.Show("Error details copied to clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception)
            {
                MessageBox.Show("Failed to copy to clipboard. Please try again.", "Copy Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
