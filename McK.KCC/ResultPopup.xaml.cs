using System;
using System.Windows;

namespace McK.KCC
{
    /// <summary>
    /// The "yay it worked!" popup. Shows success messages after registry or IIS
    /// operations complete. Has a fun button that says "Change Confirmed" because
    /// regular buttons are boring. Everyone likes confirmation, especialy IT folk.
    /// </summary>
    public partial class ResultPopup : Window
    {
        // Fallback values in case someone passes empty strings
        // Hey it could happen, you never know
        private const string DefaultTitle = "Success";
        private const string DefaultMessage = "Operation completed successfully.";

        /// <summary>
        /// Creates a success popup with the given title and message.
        /// Falls back to defaults if you pass null or whitespace because
        /// a blank success dialog is just wierd.
        /// </summary>
        /// <param name="title">Short success title</param>
        /// <param name="message">Longer descriptive message</param>
        public ResultPopup(string title, string message)
        {
            InitializeComponent();
            
            // Validate inputs and use defaults if empty
            if (string.IsNullOrWhiteSpace(title))
            {
                title = DefaultTitle;
            }
            
            if (string.IsNullOrWhiteSpace(message))
            {
                message = DefaultMessage;
            }
            
            ResultTitle.Text = title;
            ResultMessage.Text = message;
        }

        /// <summary>
        /// Handles the "Huzzah" button click. Well actualy its labeled
        /// "Change Confirmed" but Huzzah would be funnier. Closes the dialog.
        /// </summary>
        private void BtnHuzzah_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
