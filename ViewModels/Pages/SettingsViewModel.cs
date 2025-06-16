using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Modrix.Models;
using Modrix.Services;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using System.ComponentModel;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxButton = Wpf.Ui.Controls.MessageBoxButton;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;
using TextBlock = System.Windows.Controls.TextBlock;
using IThemeService = Modrix.Services.IThemeService;

namespace Modrix.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private readonly IThemeService _themeService;
        private readonly IConfiguration _configuration;
        private readonly IContentDialogService _dialogService;
        private readonly JdkHelper _jdkHelper;
        private bool _isInitialized = false;

        [ObservableProperty]
        private string _appVersion = String.Empty;

        [ObservableProperty]
        private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

        [ObservableProperty]
        private ObservableCollection<JdkHelper.JdkInfo> _installedJdks = new();

        [ObservableProperty]
        private bool _isInstallingJdk;

        [ObservableProperty]
        private int _installationProgress;

        [ObservableProperty]
        private string _installationStatus = "Ready";

        [ObservableProperty]
        private IdeSettings _ideSettings = new();

        [ObservableProperty]
        private ObservableCollection<string> _monospaceFonts = new();

        [ObservableProperty]
        private ObservableCollection<string> _encodingOptions = new();

        public SettingsViewModel(
            IThemeService themeService,
            IContentDialogService dialogService,
            IConfiguration configuration)
        {
            _themeService = themeService;
            _dialogService = dialogService;
            _configuration = configuration;
            _jdkHelper = new JdkHelper();

            if (!_isInitialized)
                InitializeViewModel();
        }

        private void InitializeViewModel()
        {
            AppVersion = GetAssemblyVersion();
            CurrentTheme = _themeService.LoadTheme();

            LoadMonospaceFonts();
            LoadEncodingOptions();
            LoadIdeSettings();
            RefreshJdks();

            _isInitialized = true;
        }

        private void LoadMonospaceFonts()
        {
            var monoFonts = new[] 
            { 
                "Cascadia Code", 
                "Consolas", 
                "Courier New", 
                "Fira Code", 
                "JetBrains Mono", 
                "Lucida Console", 
                "Monaco", 
                "Monospace" 
            };

            MonospaceFonts = new ObservableCollection<string>(
                monoFonts.Where(font => Fonts.SystemFontFamilies
                    .Any(f => f.Source.Equals(font, StringComparison.OrdinalIgnoreCase)))
            );

            if (!MonospaceFonts.Contains(IdeSettings.FontFamily))
            {
                IdeSettings.FontFamily = MonospaceFonts.FirstOrDefault() ?? "Consolas";
            }
        }

        private void LoadEncodingOptions()
        {
            EncodingOptions = new ObservableCollection<string>
            {
                "UTF-8",
                "UTF-16",
                "ASCII",
                "Windows-1252"
            };
        }

        private void LoadIdeSettings()
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Modrix",
                "appsettings.json"
            );

            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(json);
                IdeSettings = config?.IdeSettings ?? new IdeSettings();
            }
            else
            {
                IdeSettings = new IdeSettings();
            }

            // Subscribe to changes
            if (IdeSettings is INotifyPropertyChanged notifyPropertyChanged)
            {
                notifyPropertyChanged.PropertyChanged += (s, e) => SaveIdeSettings();
            }
        }

        private void SaveIdeSettings()
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Modrix",
                "appsettings.json"
            );

            var config = new AppConfig { IdeSettings = IdeSettings };
            var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            File.WriteAllText(configPath, json);
        }

        private string GetAssemblyVersion()
        {
            return typeof(App).Assembly.GetName()?.Version?.ToString() ?? String.Empty;
        }

        [RelayCommand]
        private void OnChangeTheme(string parameter)
        {
            switch (parameter)
            {
                case "theme_light":
                    if (CurrentTheme == ApplicationTheme.Light)
                        break;
                    
                    CurrentTheme = ApplicationTheme.Light;
                    ApplicationThemeManager.Apply(CurrentTheme);
                    _themeService.SaveTheme(CurrentTheme);
                    break;

                case "theme_dark":
                    if (CurrentTheme == ApplicationTheme.Dark)
                        break;
                    
                    CurrentTheme = ApplicationTheme.Dark;
                    ApplicationThemeManager.Apply(CurrentTheme);
                    _themeService.SaveTheme(CurrentTheme);
                    break;

                case "theme_highcontrast":
                    if (CurrentTheme == ApplicationTheme.HighContrast)
                        break;
                    
                    CurrentTheme = ApplicationTheme.HighContrast;
                    ApplicationThemeManager.Apply(CurrentTheme);
                    _themeService.SaveTheme(CurrentTheme);
                    break;
            }
        }

        public async Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();

            RefreshJdks();
            await Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync()
        {
            return Task.CompletedTask;
        }

        [RelayCommand]
        private void RefreshJdks()
        {
            InstalledJdks = new ObservableCollection<JdkHelper.JdkInfo>(_jdkHelper.GetInstalledJdks());
        }

        [RelayCommand]
        private void OpenJdkFolder(JdkHelper.JdkInfo jdk)
        {
            if (Directory.Exists(jdk.Path))
            {
                Process.Start("explorer.exe", jdk.Path);
            }
        }

        [RelayCommand]
        private async Task RemoveJdk(JdkHelper.JdkInfo jdk)
        {
            if (!jdk.IsRemovable) return;

            var dialog = new MessageBox
            {
                Title = "Remove JDK",
                Content = $"Are you sure you want to remove {jdk.Version}?",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel"
            };

            if (await dialog.ShowDialogAsync() == MessageBoxResult.Primary)
            {
                try
                {
                    Directory.Delete(jdk.Path, true);
                    RefreshJdks();
                }
                catch (Exception ex)
                {
                    await new MessageBox
                    {
                        Title = "Error",
                        Content = $"Failed to remove JDK: {ex.Message}",
                        PrimaryButtonText = "OK"
                    }.ShowDialogAsync();
                }
            }
        }

        [RelayCommand]
        private async Task DownloadJdkAsync()
        {
            var versions = _jdkHelper.GetAvailableJdkVersions();
            if (!versions.Any())
            {
                await new MessageBox
                {
                    Title = "No Downloads Available",
                    Content = "No JDK versions are available for download at this time.",
                    PrimaryButtonText = "OK"
                }.ShowDialogAsync();
                return;
            }

            var selectedVersion = -1;
            var stackPanel = new StackPanel { Margin = new System.Windows.Thickness(0, 10, 0, 0) };
            var comboBox = new ComboBox
            {
                ItemsSource = versions,
                SelectedIndex = 0,
                Width = 200,
                Margin = new System.Windows.Thickness(0, 5, 0, 0)
            };
            
            comboBox.SelectionChanged += (s, e) => selectedVersion = (int)comboBox.SelectedItem;

            stackPanel.Children.Add(new TextBlock { Text = "Select JDK version to download:" });
            stackPanel.Children.Add(comboBox);

            var options = new SimpleContentDialogCreateOptions
            {
                Title = "Download JDK",
                Content = stackPanel,
                PrimaryButtonText = "Download",
                CloseButtonText = "Cancel"
            };

            var result = await _dialogService.ShowSimpleDialogAsync(options);

            if (result == ContentDialogResult.Primary && selectedVersion != -1)
            {
                try
                {
                    IsInstallingJdk = true;
                    await InstallJdkAsync(selectedVersion);
                }
                finally
                {
                    IsInstallingJdk = false;
                    InstallationStatus = "Ready";
                    InstallationProgress = 0;
                }
            }
        }

        private async Task InstallJdkAsync(int version)
        {
            try
            {
                var progress = new Progress<(string Message, int Progress)>(update =>
                {
                    InstallationStatus = update.Message;
                    InstallationProgress = update.Progress;
                });

                await _jdkHelper.DownloadAndInstallJdkAsync(version, progress);
                RefreshJdks();

                await new MessageBox
                {
                    Title = "Success",
                    Content = $"JDK {version} was installed successfully.",
                    PrimaryButtonText = "OK"
                }.ShowDialogAsync();
            }
            catch (Exception ex)
            {
                await new MessageBox
                {
                    Title = "Installation Failed",
                    Content = $"Failed to install JDK {version}: {ex.Message}",
                    PrimaryButtonText = "OK"
                }.ShowDialogAsync();
            }
        }
    }
}
