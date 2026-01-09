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
    /// <summary>
    /// Main window for the Keeper Config Creator. This is where all the magic happens.
    /// By magic I mean registry manipulation and IIS config tweaks. So exciting right?
    /// </summary>
    public partial class MainWindow : Window
    {
        // These are readonly because we load em once and dont touch em again
        // If you change these at runtime something has gone horribly wrong
        private readonly string _variableName;
        private readonly string _variableValue;
        
        // Track wether our registry entries exist in each scope
        // Updated every time we mess with the registry
        private bool _userScopeExists;
        private bool _systemScopeExists;

        /// <summary>
        /// Constructor. Loads config, sets up the UI, you know the drill.
        /// If the config file is missing or broken, we default to sane values
        /// because crashing on startup is a bad look.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            
            // Attempt to load our config from JSON. If this fails we fall back to defaults
            // because somtimes IT forgets to copy the config file. Ask me how I know.
            try
            {
                // Build the path using the app's base directory for portability
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                var configJson = File.ReadAllText(configPath);
                
                // Using camelCase policy to match our JSON naming convention
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var config = JsonSerializer.Deserialize<KeeperConfig>(configJson, options);
                
                // Null coalescing for the win. Defaults if config is null or empty.
                _variableName = config?.VariableName ?? "KeeperConfig";
                _variableValue = config?.VariableValue ?? string.Empty;
            }
            catch (Exception ex)
            {
                // Show the error but dont crash. User needs to see whats wrong.
                ShowError("Failed to load configuration", ex.Message);
                _variableName = "KeeperConfig";
                _variableValue = string.Empty;
            }
            
            // Refresh the UI to show current registry state
            UpdateLocalRegistrationUI();
            
            // Load IIS sites if IIS is installed. If not, the controls get disabled.
            LoadIISSites();
        }

        /// <summary>
        /// Refreshes the UI to show the current state of registry entries.
        /// Checks both User and System scope and updates the pretty colored borders
        /// because visual feedback matters, even for boring registry stuff.
        /// </summary>
        private void UpdateLocalRegistrationUI()
        {
            // Check User scope in HKEY_CURRENT_USER\Environment
            // This is where user level env vars live
            _userScopeExists = CheckRegistryEntry(RegistryHive.CurrentUser, 
                @"Environment", _variableName);
            
            // Check System scope in HKLM. Yes that path is ridiculously long.
            // Someone at Microsoft really wanted to be specific I guess.
            _systemScopeExists = CheckRegistryEntry(RegistryHive.LocalMachine, 
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", _variableName);
            
            // Update the User Scope card with apropriate colors and button text
            if (_userScopeExists)
            {
                // Green means go... or in this case, present. Same diff.
                UserScopeBorder.BorderBrush = (SolidColorBrush)FindResource("GreenBrush");
                BtnUserScope.Style = (Style)FindResource("DangerButton");
                BtnUserScopeText.Text = "REMOVE";
                UserScopeText.Text = "Scope: User - Present";
            }
            else
            {
                // Yellow/orange for warning. Its not there yet bucko.
                UserScopeBorder.BorderBrush = (SolidColorBrush)FindResource("WarningBrush");
                BtnUserScope.Style = (Style)FindResource("SuccessButton");
                BtnUserScopeText.Text = "UPDATE";
                UserScopeText.Text = "Scope: User - Not Present";
            }
            
            // Same logic for System scope. Copy pasta with extra cheese.
            if (_systemScopeExists)
            {
                SystemScopeBorder.BorderBrush = (SolidColorBrush)FindResource("GreenBrush");
                BtnSystemScope.Style = (Style)FindResource("DangerButton");
                BtnSystemScopeText.Text = "REMOVE";
                SystemScopeText.Text = "Scope: System - Present";
            }
            else
            {
                SystemScopeBorder.BorderBrush = (SolidColorBrush)FindResource("WarningBrush");
                BtnSystemScope.Style = (Style)FindResource("SuccessButton");
                BtnSystemScopeText.Text = "UPDATE";
                SystemScopeText.Text = "Scope: System - Not Present";
            }
        }

        /// <summary>
        /// Checks if a registry entry exists. Returns true if found, false otherwise.
        /// Handles security exceptions gracefuly because not everyone has admin rights.
        /// Who knew right?
        /// </summary>
        /// <param name="hive">Which hive to check (HKCU or HKLM usualy)</param>
        /// <param name="subKey">The subkey path to look in</param>
        /// <param name="valueName">Name of the value were hunting for</param>
        /// <returns>True if the value exists, false if not or we cant access it</returns>
        private bool CheckRegistryEntry(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                // Using the 64 bit view to be consistent across platforms
                // 32 bit apps on 64 bit Windows get redirected otherwise. Fun times.
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(subKey, false);
                
                if (key == null) return false;
                
                var value = key.GetValue(valueName);
                return value != null;
            }
            catch (System.Security.SecurityException)
            {
                // Nope, user cant even peek at this key. Sad.
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // Also nope. Different exception, same result.
                return false;
            }
        }

        /// <summary>
        /// Handles clicks on the User Scope button. Either removes or adds the entry
        /// depending on wether it already exists. Confirmation dialogs included because
        /// nobody wants to accidentally nuke their config. Been there done that.
        /// </summary>
        private void BtnUserScope_Click(object sender, RoutedEventArgs e)
        {
            if (_userScopeExists)
            {
                // Entry exists, user wants to remove it. Make em confirm first.
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
                // Entry doesnt exist, user wants to add it. Still confirm tho.
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

        /// <summary>
        /// Handles clicks on the System Scope button. Same deal as user scope but
        /// requires admin privleges. If you dont have em, youll find out real quick.
        /// </summary>
        private void BtnSystemScope_Click(object sender, RoutedEventArgs e)
        {
            if (_systemScopeExists)
            {
                // Remove the system level entry. This is the scary one.
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
                // Add the system level entry. Also scary but in a different way.
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

        /// <summary>
        /// Sets a registry value. The actual write operation. Uses writable handle.
        /// If this fails, user sees an error. No silent failures here.
        /// </summary>
        /// <param name="hive">Target registry hive</param>
        /// <param name="subKey">Subkey path</param>
        /// <param name="valueName">Value name to write</param>
        /// <param name="value">The data to write</param>
        private void SetRegistryEntry(RegistryHive hive, string subKey, string valueName, string value)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(subKey, true); // true for writeable
                
                if (key == null)
                {
                    ShowError("Registry Error", $"Unable to open registry key: {subKey}");
                    return;
                }
                
                // Write it as a string value. Keep it simple.
                key.SetValue(valueName, value, RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                ShowError("Failed to set registry entry", ex.Message);
            }
        }

        /// <summary>
        /// Removes a registry value. Opposite of SetRegistryEntry, shockingly.
        /// The false parameter means dont throw if the value doesnt exist.
        /// </summary>
        private void RemoveRegistryEntry(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(subKey, true);
                
                if (key == null)
                {
                    ShowError("Registry Error", $"Unable to open registry key: {subKey}");
                    return;
                }
                
                // DeleteValue with false = dont throw if it doesnt exist
                key.DeleteValue(valueName, false);
            }
            catch (Exception ex)
            {
                ShowError("Failed to remove registry entry", ex.Message);
            }
        }

        /// <summary>
        /// Loads all IIS sites into the combobox. If IIS isnt installed (gasp!)
        /// the controls get disabled. Users without IIS dont need this tab anyway.
        /// Sites are sorted with those needing config first for convienence.
        /// </summary>
        private void LoadIISSites()
        {
            try
            {
                var sites = new List<IISSiteInfo>();
                
                // ServerManager is the IIS management API entry point
                // Disposable because it holds COM resources under the hood
                using var serverManager = new ServerManager();
                
                foreach (var site in serverManager.Sites)
                {
                    // Check each site to see if it already has our app setting
                    var hasAppSetting = CheckIISAppSetting(site);
                    sites.Add(new IISSiteInfo
                    {
                        Name = site.Name,
                        HasAppSetting = hasAppSetting
                    });
                }
                
                // Sort with sites missing config first, then alphabeticaly within groups
                // This way users see what needs attention right at the top
                var sortedSites = sites.OrderBy(s => s.HasAppSetting).ThenBy(s => s.Name).ToList();
                
                // Build a fancy list with headers and separators for the dropdown
                // because plain lists are boring and hard to scan
                var itemsWithSeparator = new List<object>();
                
                // First item is always "Select Site" as a placeholder
                itemsWithSeparator.Add(new IISSiteDisplayItem
                {
                    DisplayName = "Select Site",
                    TextColor = (SolidColorBrush)this.FindResource("TextSecondaryBrush"),
                    Site = null,
                    IsSeparator = true,
                    FontWeight = FontWeights.Normal
                });
                
                // Split into two groups for better organization
                var sitesWithoutSetting = sortedSites.Where(s => !s.HasAppSetting).ToList();
                var sitesWithSetting = sortedSites.Where(s => s.HasAppSetting).ToList();
                
                // Add header for sites that need the config (warning color)
                if (sitesWithoutSetting.Count > 0)
                {
                    itemsWithSeparator.Add(new IISSiteDisplayItem
                    {
                        DisplayName = "━━ Sites Without Keeper Config (Needs to be Added) ━━",
                        TextColor = (SolidColorBrush)this.FindResource("WarningBrush"),
                        Site = null,
                        IsSeparator = true,
                        FontWeight = FontWeights.Bold
                    });
                    
                    // Add each site with indentation for visual hierarchy
                    foreach (var site in sitesWithoutSetting)
                    {
                        itemsWithSeparator.Add(new IISSiteDisplayItem
                        {
                            DisplayName = "   " + site.Name,
                            TextColor = (SolidColorBrush)this.FindResource("WarningBrush"),
                            Site = site
                        });
                    }
                }
                
                // Add header for sites that already have config (green = good)
                if (sitesWithSetting.Count > 0)
                {
                    itemsWithSeparator.Add(new IISSiteDisplayItem
                    {
                        DisplayName = "━━ Sites With Keeper Config (Already Added) ━━",
                        TextColor = (SolidColorBrush)this.FindResource("GreenBrush"),
                        Site = null,
                        IsSeparator = true,
                        FontWeight = FontWeights.Bold
                    });
                    
                    foreach (var site in sitesWithSetting)
                    {
                        itemsWithSeparator.Add(new IISSiteDisplayItem
                        {
                            DisplayName = "   " + site.Name,
                            TextColor = (SolidColorBrush)this.FindResource("GreenBrush"),
                            Site = site
                        });
                    }
                }
                
                CmbIISSites.ItemsSource = itemsWithSeparator;
                CmbIISSites.SelectedIndex = 0; // Default to the placeholder
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // IIS not installed or not accessible. Disable the controls.
                CmbIISSites.IsEnabled = false;
                BtnIISSite.IsEnabled = false;
            }
            catch (UnauthorizedAccessException)
            {
                // User cant access IIS config. Need more perms.
                CmbIISSites.IsEnabled = false;
                BtnIISSite.IsEnabled = false;
            }
            catch (Exception)
            {
                // Catch all for other IIS related failures
                // Probably means IIS isnt installed which is fine
                CmbIISSites.IsEnabled = false;
                BtnIISSite.IsEnabled = false;
            }
        }

        /// <summary>
        /// Checks if a specific IIS site has our app setting configured.
        /// Digs through the web.config appSettings section looking for our key.
        /// </summary>
        /// <param name="site">The IIS site to check</param>
        /// <returns>True if the setting exists, false otherwise</returns>
        private bool CheckIISAppSetting(Site site)
        {
            try
            {
                var config = site.GetWebConfiguration();
                var appSettings = config.GetSection("appSettings");
                var settings = appSettings.GetCollection();
                
                // Loop through looking for our variable name
                // Could use LINQ here but the explicit loop is clearer IMO
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
                // No web.config for this site. Thats ok, it just means no setting.
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // Cant read the config. Assume no setting since we cant verify.
                return false;
            }
        }

        /// <summary>
        /// Handles selection changes in the IIS sites dropdown.
        /// Shows or hides the action button based on what was selected.
        /// </summary>
        private void CmbIISSites_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Make sure its a valid site selection, not a header or separator
            if (CmbIISSites.SelectedItem is IISSiteDisplayItem displayItem && !displayItem.IsSeparator && displayItem.Site != null)
            {
                var site = displayItem.Site;
                BtnIISSite.Visibility = Visibility.Visible;
                
                // Change button style based on current state
                if (site.HasAppSetting)
                {
                    BtnIISSite.Style = (Style)FindResource("DangerButton");
                    BtnIISSiteText.Text = "REMOVE";
                }
                else
                {
                    BtnIISSite.Style = (Style)FindResource("SuccessButton");
                    BtnIISSiteText.Text = "UPDATE";
                }
            }
            else
            {
                // Invalid selection, hide the button
                BtnIISSite.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Handles clicks on the IIS site action button.
        /// Either adds or removes the app setting depending on current state.
        /// </summary>
        private void BtnIISSite_Click(object sender, RoutedEventArgs e)
        {
            if (CmbIISSites.SelectedItem is IISSiteDisplayItem displayItem && !displayItem.IsSeparator && displayItem.Site != null)
            {
                var site = displayItem.Site;
                
                if (site.HasAppSetting)
                {
                    // Confirm before removing. You know the drill.
                    var result = MessageBox.Show(
                        $"Are you sure you want to remove the '{_variableName}' app setting from '{site.Name}'?",
                        "Confirm Removal",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        RemoveIISAppSetting(site.Name);
                        ShowSuccess("App Setting Removed", $"'{_variableName}' has been removed from '{site.Name}'.");
                        LoadIISSites(); // Refresh the list
                    }
                }
                else
                {
                    // Confirm before adding
                    var result = MessageBox.Show(
                        $"Are you sure you want to add/update the '{_variableName}' app setting for '{site.Name}'?",
                        "Confirm Update",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        SetIISAppSetting(site.Name);
                        ShowSuccess("App Setting Updated", $"'{_variableName}' has been added/updated for '{site.Name}'.");
                        LoadIISSites(); // Refresh the list
                    }
                }
            }
        }

        /// <summary>
        /// Sets or updates an app setting in an IIS sites web.config.
        /// Creates a new setting if it doesnt exist, updates if it does.
        /// Changes are commited to disk imediately.
        /// </summary>
        /// <param name="siteName">Name of the IIS site to modify</param>
        private void SetIISAppSetting(string siteName)
        {
            try
            {
                using var serverManager = new ServerManager();
                
                var site = serverManager.Sites[siteName];
                if (site == null)
                {
                    ShowError("IIS Error", $"Site '{siteName}' not found.");
                    return;
                }
                
                var config = site.GetWebConfiguration();
                var appSettings = config.GetSection("appSettings");
                var settings = appSettings.GetCollection();
                
                // Look for existing setting to update
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
                    // Update the existing one
                    existingSetting.SetAttributeValue("value", _variableValue);
                }
                else
                {
                    // Create a new setting element
                    var newSetting = settings.CreateElement("add");
                    newSetting.SetAttributeValue("key", _variableName);
                    newSetting.SetAttributeValue("value", _variableValue);
                    settings.Add(newSetting);
                }
                
                // Commit changes writes to web.config on disk
                serverManager.CommitChanges();
            }
            catch (Exception ex)
            {
                ShowError("Failed to set IIS app setting", ex.Message);
            }
        }

        /// <summary>
        /// Removes an app setting from an IIS sites web.config.
        /// If the setting doesnt exist, this does nothing without error.
        /// </summary>
        /// <param name="siteName">Name of the IIS site to modify</param>
        private void RemoveIISAppSetting(string siteName)
        {
            try
            {
                using var serverManager = new ServerManager();
                
                var site = serverManager.Sites[siteName];
                if (site == null)
                {
                    ShowError("IIS Error", $"Site '{siteName}' not found.");
                    return;
                }
                
                var config = site.GetWebConfiguration();
                var appSettings = config.GetSection("appSettings");
                var settings = appSettings.GetCollection();
                
                // Find the setting to remove
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
                // If not found, we just silently succeed. Its already gone.
            }
            catch (Exception ex)
            {
                ShowError("Failed to remove IIS app setting", ex.Message);
            }
        }

        /// <summary>
        /// Shows a success popup with the given title and message.
        /// Sets the owner so it appears centered on the main window.
        /// </summary>
        private void ShowSuccess(string title, string message)
        {
            var resultPopup = new ResultPopup(title, message);
            resultPopup.Owner = this;
            resultPopup.ShowDialog();
        }

        /// <summary>
        /// Shows an error popup with the given title and message.
        /// Because things go wrong and users need to know whats up.
        /// </summary>
        private void ShowError(string title, string message)
        {
            var errorWindow = new ErrorWindow(title, message);
            errorWindow.Owner = this;
            errorWindow.ShowDialog();
        }
    }

    /// <summary>
    /// Configuration model for Keeper settings. Loaded from config.json.
    /// Just two fields: the variable name and its value. Simple as.
    /// </summary>
    public class KeeperConfig
    {
        /// <summary>
        /// Name of the enviroment variable or app setting key
        /// </summary>
        public string? VariableName { get; set; }
        
        /// <summary>
        /// The actual value to store. Usualy base64 encoded JSON.
        /// </summary>
        public string? VariableValue { get; set; }
    }

    /// <summary>
    /// Represents basic info about an IIS site.
    /// Tracks wether the site already has our app setting or not.
    /// </summary>
    public class IISSiteInfo
    {
        /// <summary>Site name as shown in IIS Manager</summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>True if the site already has our KeeperConfig app setting</summary>
        public bool HasAppSetting { get; set; }
    }

    /// <summary>
    /// Display model for the IIS sites combobox. Includes styling info
    /// so we can have fancy colored headers and separators in the dropdown.
    /// A bit over engineered maybe but it looks nice so whatever.
    /// </summary>
    public class IISSiteDisplayItem
    {
        /// <summary>Text shown in the dropdown</summary>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>Color of the text, changes based on config status</summary>
        public SolidColorBrush TextColor { get; set; } = new SolidColorBrush(Colors.White);
        
        /// <summary>Reference to the actual site info, null for headers</summary>
        public IISSiteInfo? Site { get; set; }
        
        /// <summary>True if this item is a header/separator, not selectable</summary>
        public bool IsSeparator { get; set; }
        
        /// <summary>Font weight, bold for headers</summary>
        public FontWeight FontWeight { get; set; } = FontWeights.Normal;
    }
}
