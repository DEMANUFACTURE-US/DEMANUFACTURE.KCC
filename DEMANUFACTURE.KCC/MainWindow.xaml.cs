using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;

namespace DEMANUFACTURE.KCC
{
    public partial class MainWindow : Window
    {
        private readonly string _variableName;
        private readonly string _variableValue;

        public MainWindow()
        {
            InitializeComponent();
            
            // Load configuration from JSON file
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                var configJson = File.ReadAllText(configPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var config = JsonSerializer.Deserialize<KeeperConfig>(configJson, options);
                
                _variableName = config?.VariableName ?? "KeeperConfig";
                _variableValue = config?.VariableValue ?? string.Empty;
            }
            catch (Exception ex)
            {
                ShowError("Failed to load configuration", ex.Message);
                _variableName = "KeeperConfig";
                _variableValue = string.Empty;
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool userExisted = false;
                bool systemExisted = false;
                
                // Check if variables already exist
                var existingUserValue = Environment.GetEnvironmentVariable(_variableName, EnvironmentVariableTarget.User);
                var existingSystemValue = Environment.GetEnvironmentVariable(_variableName, EnvironmentVariableTarget.Machine);
                
                userExisted = !string.IsNullOrEmpty(existingUserValue);
                systemExisted = !string.IsNullOrEmpty(existingSystemValue);
                
                // Set User scope environment variable
                Environment.SetEnvironmentVariable(_variableName, _variableValue, EnvironmentVariableTarget.User);
                
                // Set System scope environment variable
                Environment.SetEnvironmentVariable(_variableName, _variableValue, EnvironmentVariableTarget.Machine);
                
                // Show success status
                ShowSuccess(userExisted, systemExisted);
            }
            catch (Exception ex)
            {
                ShowError("Failed to set environment variable", ex.Message);
            }
        }

        private void ShowSuccess(bool userExisted, bool systemExisted)
        {
            StatusBorder.Visibility = Visibility.Visible;
            StatusBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x2F));
            StatusIcon.Text = "✓";
            StatusIcon.Foreground = (Brush)FindResource("SuccessBrush");
            StatusTitle.Foreground = (Brush)FindResource("SuccessBrush");
            
            string userAction = userExisted ? "overwritten" : "created";
            string systemAction = systemExisted ? "overwritten" : "created";
            
            if (userExisted || systemExisted)
            {
                StatusTitle.Text = "Environment Variable Updated";
                StatusMessage.Text = $"User scope: {userAction}\nSystem scope: {systemAction}";
            }
            else
            {
                StatusTitle.Text = "Environment Variable Created";
                StatusMessage.Text = $"{_variableName} has been added to both User and System scopes.";
            }
        }

        private void ShowError(string title, string message)
        {
            var errorWindow = new ErrorWindow(title, message);
            errorWindow.Owner = this;
            errorWindow.ShowDialog();
        }
    }

    public class KeeperConfig
    {
        public string? VariableName { get; set; }
        public string? VariableValue { get; set; }
    }
}
