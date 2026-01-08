using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Microsoft.Web.Administration;

namespace McK.KCC
{
    public partial class MainWindow : Window
    {
        private readonly string _variableName;
        private readonly string _variableValue;
        private bool _userScopeExists;
        private bool _systemScopeExists;

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
            
            // Initialize the UI
            UpdateLocalRegistrationUI();
            LoadIISSites();
        }

        private void UpdateLocalRegistrationUI()
        {
            // Check User scope registry
            _userScopeExists = CheckRegistryEntry(RegistryHive.CurrentUser, 
                @"Environment", _variableName);
            
            // Check System scope registry
            _systemScopeExists = CheckRegistryEntry(RegistryHive.LocalMachine, 
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", _variableName);
            
            // Update User Scope UI
            if (_userScopeExists)
            {
                UserScopeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(40, 167, 69)); // Green
                BtnUserScope.Style = (Style)FindResource("DangerButton");
                BtnUserScopeText.Text = "REMOVE";
            }
            else
            {
                UserScopeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 166, 35)); // Orange
                BtnUserScope.Style = (Style)FindResource("SuccessButton");
                BtnUserScopeText.Text = "UPDATE";
            }
            
            // Update System Scope UI
            if (_systemScopeExists)
            {
                SystemScopeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(40, 167, 69)); // Green
                BtnSystemScope.Style = (Style)FindResource("DangerButton");
                BtnSystemScopeText.Text = "REMOVE";
            }
            else
            {
                SystemScopeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 166, 35)); // Orange
                BtnSystemScope.Style = (Style)FindResource("SuccessButton");
                BtnSystemScopeText.Text = "UPDATE";
            }
        }

        private bool CheckRegistryEntry(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(subKey, false))
                {
                    if (key == null) return false;
                    var value = key.GetValue(valueName);
                    return value != null;
                }
            }
            catch (System.Security.SecurityException)
            {
                // User doesn't have permission to access this registry key
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // Access denied to this registry key
                return false;
            }
        }

        private void BtnUserScope_Click(object sender, RoutedEventArgs e)
        {
            if (_userScopeExists)
            {
                // Show confirmation dialog for REMOVE
                var result = MessageBox.Show(
                    $"Are you sure you want to remove the '{_variableName}' registry entry from User scope?",
                    "Confirm Removal",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    RemoveRegistryEntry(RegistryHive.CurrentUser, @"Environment", _variableName);
                    ShowSuccess("Registry Entry Removed", $"'{_variableName}' has been removed from User scope.");
                    UpdateLocalRegistrationUI();
                }
            }
            else
            {
                // Show confirmation dialog for UPDATE
                var result = MessageBox.Show(
                    $"Are you sure you want to add/update the '{_variableName}' registry entry in User scope?",
                    "Confirm Update",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    SetRegistryEntry(RegistryHive.CurrentUser, @"Environment", _variableName, _variableValue);
                    ShowSuccess("Registry Entry Updated", $"'{_variableName}' has been added/updated in User scope.");
                    UpdateLocalRegistrationUI();
                }
            }
        }

        private void BtnSystemScope_Click(object sender, RoutedEventArgs e)
        {
            if (_systemScopeExists)
            {
                // Show confirmation dialog for REMOVE
                var result = MessageBox.Show(
                    $"Are you sure you want to remove the '{_variableName}' registry entry from System scope?",
                    "Confirm Removal",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    RemoveRegistryEntry(RegistryHive.LocalMachine, 
                        @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", _variableName);
                    ShowSuccess("Registry Entry Removed", $"'{_variableName}' has been removed from System scope.");
                    UpdateLocalRegistrationUI();
                }
            }
            else
            {
                // Show confirmation dialog for UPDATE
                var result = MessageBox.Show(
                    $"Are you sure you want to add/update the '{_variableName}' registry entry in System scope?",
                    "Confirm Update",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    SetRegistryEntry(RegistryHive.LocalMachine, 
                        @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", _variableName, _variableValue);
                    ShowSuccess("Registry Entry Updated", $"'{_variableName}' has been added/updated in System scope.");
                    UpdateLocalRegistrationUI();
                }
            }
        }

        private void SetRegistryEntry(RegistryHive hive, string subKey, string valueName, string value)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(subKey, true))
                {
                    if (key == null)
                    {
                        ShowError("Registry Error", $"Unable to open registry key: {subKey}");
                        return;
                    }
                    key.SetValue(valueName, value, RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                ShowError("Failed to set registry entry", ex.Message);
            }
        }

        private void RemoveRegistryEntry(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(subKey, true))
                {
                    if (key == null)
                    {
                        ShowError("Registry Error", $"Unable to open registry key: {subKey}");
                        return;
                    }
                    key.DeleteValue(valueName, false);
                }
            }
            catch (Exception ex)
            {
                ShowError("Failed to remove registry entry", ex.Message);
            }
        }

        private void LoadIISSites()
        {
            try
            {
                var sites = new List<IISSiteInfo>();
                
                using (var serverManager = new ServerManager())
                {
                    foreach (var site in serverManager.Sites)
                    {
                        var hasAppSetting = CheckIISAppSetting(site);
                        sites.Add(new IISSiteInfo
                        {
                            Name = site.Name,
                            HasAppSetting = hasAppSetting
                        });
                    }
                }
                
                // Sort sites: those without app settings first, then those with
                var sortedSites = sites.OrderBy(s => s.HasAppSetting).ThenBy(s => s.Name).ToList();
                
                // Add separator item
                var itemsWithSeparator = new List<object>();
                var sitesWithoutSetting = sortedSites.Where(s => !s.HasAppSetting).ToList();
                var sitesWithSetting = sortedSites.Where(s => s.HasAppSetting).ToList();
                
                foreach (var site in sitesWithoutSetting)
                {
                    itemsWithSeparator.Add(new IISSiteDisplayItem
                    {
                        DisplayName = site.Name,
                        TextColor = new SolidColorBrush(Color.FromRgb(245, 166, 35)), // Orange
                        Site = site
                    });
                }
                
                if (sitesWithoutSetting.Any() && sitesWithSetting.Any())
                {
                    itemsWithSeparator.Add(new IISSiteDisplayItem
                    {
                        DisplayName = "─────────────────────",
                        TextColor = new SolidColorBrush(Color.FromRgb(122, 138, 154)), // Muted
                        Site = null,
                        IsSeparator = true
                    });
                }
                
                foreach (var site in sitesWithSetting)
                {
                    itemsWithSeparator.Add(new IISSiteDisplayItem
                    {
                        DisplayName = site.Name,
                        TextColor = new SolidColorBrush(Color.FromRgb(40, 167, 69)), // Green
                        Site = site
                    });
                }
                
                CmbIISSites.ItemsSource = itemsWithSeparator;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // IIS might not be installed or accessible
                CmbIISSites.IsEnabled = false;
                BtnIISSite.IsEnabled = false;
            }
            catch (UnauthorizedAccessException)
            {
                // Don't have permission to access IIS
                CmbIISSites.IsEnabled = false;
                BtnIISSite.IsEnabled = false;
            }
            catch (Exception)
            {
                // Other error loading IIS sites - likely IIS not installed
                CmbIISSites.IsEnabled = false;
                BtnIISSite.IsEnabled = false;
            }
        }

        private bool CheckIISAppSetting(Site site)
        {
            try
            {
                var config = site.GetWebConfiguration();
                var appSettings = config.GetSection("appSettings");
                var settings = appSettings.GetCollection();
                
                foreach (var setting in settings)
                {
                    var key = setting.GetAttributeValue("key")?.ToString();
                    if (key == _variableName)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (System.IO.FileNotFoundException)
            {
                // Configuration file not found for this site
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // Don't have permission to read the configuration
                return false;
            }
        }

        private void CmbIISSites_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbIISSites.SelectedItem is IISSiteDisplayItem displayItem && !displayItem.IsSeparator && displayItem.Site != null)
            {
                var site = displayItem.Site;
                BtnIISSite.IsEnabled = true;
                
                if (site.HasAppSetting)
                {
                    BtnIISSite.Style = (Style?)FindResource("DangerButton") ?? BtnIISSite.Style;
                    BtnIISSiteText.Text = "REMOVE";
                }
                else
                {
                    BtnIISSite.Style = (Style?)FindResource("SuccessButton") ?? BtnIISSite.Style;
                    BtnIISSiteText.Text = "UPDATE";
                }
            }
            else
            {
                BtnIISSite.IsEnabled = false;
            }
        }

        private void BtnIISSite_Click(object sender, RoutedEventArgs e)
        {
            if (CmbIISSites.SelectedItem is IISSiteDisplayItem displayItem && !displayItem.IsSeparator && displayItem.Site != null)
            {
                var site = displayItem.Site;
                
                if (site.HasAppSetting)
                {
                    // Show confirmation dialog for REMOVE
                    var result = MessageBox.Show(
                        $"Are you sure you want to remove the '{_variableName}' app setting from '{site.Name}'?",
                        "Confirm Removal",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        RemoveIISAppSetting(site.Name);
                        ShowSuccess("App Setting Removed", $"'{_variableName}' has been removed from '{site.Name}'.");
                        LoadIISSites();
                    }
                }
                else
                {
                    // Show confirmation dialog for UPDATE
                    var result = MessageBox.Show(
                        $"Are you sure you want to add/update the '{_variableName}' app setting for '{site.Name}'?",
                        "Confirm Update",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        SetIISAppSetting(site.Name);
                        ShowSuccess("App Setting Updated", $"'{_variableName}' has been added/updated for '{site.Name}'.");
                        LoadIISSites();
                    }
                }
            }
        }

        private void SetIISAppSetting(string siteName)
        {
            try
            {
                using (var serverManager = new ServerManager())
                {
                    var site = serverManager.Sites[siteName];
                    if (site == null)
                    {
                        ShowError("IIS Error", $"Site '{siteName}' not found.");
                        return;
                    }
                    
                    var config = site.GetWebConfiguration();
                    var appSettings = config.GetSection("appSettings");
                    var settings = appSettings.GetCollection();
                    
                    // Check if setting already exists
                    ConfigurationElement? existingSetting = null;
                    foreach (var setting in settings)
                    {
                        var key = setting.GetAttributeValue("key")?.ToString();
                        if (key == _variableName)
                        {
                            existingSetting = setting;
                            break;
                        }
                    }
                    
                    if (existingSetting != null)
                    {
                        // Update existing
                        existingSetting.SetAttributeValue("value", _variableValue);
                    }
                    else
                    {
                        // Add new
                        var newSetting = settings.CreateElement("add");
                        newSetting.SetAttributeValue("key", _variableName);
                        newSetting.SetAttributeValue("value", _variableValue);
                        settings.Add(newSetting);
                    }
                    
                    serverManager.CommitChanges();
                }
            }
            catch (Exception ex)
            {
                ShowError("Failed to set IIS app setting", ex.Message);
            }
        }

        private void RemoveIISAppSetting(string siteName)
        {
            try
            {
                using (var serverManager = new ServerManager())
                {
                    var site = serverManager.Sites[siteName];
                    if (site == null)
                    {
                        ShowError("IIS Error", $"Site '{siteName}' not found.");
                        return;
                    }
                    
                    var config = site.GetWebConfiguration();
                    var appSettings = config.GetSection("appSettings");
                    var settings = appSettings.GetCollection();
                    
                    // Find and remove the setting
                    ConfigurationElement? settingToRemove = null;
                    foreach (var setting in settings)
                    {
                        var key = setting.GetAttributeValue("key")?.ToString();
                        if (key == _variableName)
                        {
                            settingToRemove = setting;
                            break;
                        }
                    }
                    
                    if (settingToRemove != null)
                    {
                        settings.Remove(settingToRemove);
                        serverManager.CommitChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("Failed to remove IIS app setting", ex.Message);
            }
        }

        private void ShowSuccess(string title, string message)
        {
            var resultPopup = new ResultPopup(title, message);
            resultPopup.Owner = this;
            resultPopup.ShowDialog();
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

    public class IISSiteInfo
    {
        public string Name { get; set; } = string.Empty;
        public bool HasAppSetting { get; set; }
    }

    public class IISSiteDisplayItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public SolidColorBrush TextColor { get; set; } = new SolidColorBrush(Colors.White);
        public IISSiteInfo? Site { get; set; }
        public bool IsSeparator { get; set; }
    }
}
