using System;
using System.Windows;

namespace McK.KCC
{
    /// <summary>
    /// A friendly little error dialog that pops up when things go sideways.
    /// Has a copy button because lets be honest, no one types error messages
    /// manualy when filing bug reports. Copy paste is king.
    /// </summary>
    public partial class ErrorWindow : Window
    {
        // Store the full error details for copy functionality
        // Private because no one else needs to touch this
        private readonly string _errorDetails;

        /// <summary>
        /// Creates a new error window with the specified title and message.
        /// Concatenates them for the copy feature cuz users want all the deets.
        /// </summary>
        /// <param name="title">Brief error title like "Config Load Failed"</param>
        /// <param name="message">Detailed error message, usualy exception info</param>
        public ErrorWindow(string title, string message)
        {
            InitializeComponent();
            
            // Set up the display text
            ErrorTitle.Text = title;
            ErrorMessage.Text = message;
            
            // Build the copyable text with title and message together
            _errorDetails = $"{title}\n\n{message}";
        }

        /// <summary>
        /// Copies the error details to clipboard. Nice for sending to support
        /// or pasting into Slack to complain about broken software.
        /// </summary>
        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_errorDetails);
                MessageBox.Show("Error details copied to clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception)
            {
                // Clipboard can fail in wierd edge cases. RDP sessions, locked desktop, etc.
                // Let the user know instead of silently failing.
                MessageBox.Show("Failed to copy to clipboard. Please try again.", "Copy Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Closes the error window. Not much else to say about this one.
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
