using System;
using System.Windows;

namespace DEMANUFACTURE.KCC
{
    public partial class ResultPopup : Window
    {
        public ResultPopup(string title, string message)
        {
            InitializeComponent();
            
            if (string.IsNullOrWhiteSpace(title))
            {
                title = "Success";
            }
            
            if (string.IsNullOrWhiteSpace(message))
            {
                message = "Operation completed successfully.";
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
