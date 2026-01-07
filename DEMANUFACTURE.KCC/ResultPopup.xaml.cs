using System;
using System.Windows;

namespace DEMANUFACTURE.KCC
{
    public partial class ResultPopup : Window
    {
        private const string DefaultTitle = "Success";
        private const string DefaultMessage = "Operation completed successfully.";

        public ResultPopup(string title, string message)
        {
            InitializeComponent();
            
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

        private void BtnHuzzah_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
