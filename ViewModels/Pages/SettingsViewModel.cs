using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using Modrix.Services;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxButton = Wpf.Ui.Controls.MessageBoxButton;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Modrix.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private readonly Services.IThemeService _themeService;
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

        private readonly IContentDialogService _dialogService;
        private readonly JdkHelper _jdkHelper;


        public SettingsViewModel(Services.IThemeService themeService,
                             IContentDialogService dialogService)
        {
            _themeService = themeService;
            _dialogService = dialogService;
            _jdkHelper = new JdkHelper();
        }

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();

            return Task.CompletedTask;
        }


        [RelayCommand]
        private async Task DownloadJdkAsync()
        {
            // 1) קבל גרסאות
            var versions = _jdkHelper.GetAvailableJdkVersions();

            // 2) בנה את התוכן
            var comboBox = new ComboBox
            {
                ItemsSource = versions,
                SelectedIndex = versions.IndexOf(17) // ברירת מחדל: Java 17
            };

            var panel = new StackPanel
            {
                Children =
        {
            new TextBlock
            {
                Text   = "Select JDK version to install:",
                Margin = new Thickness(0,0,0,8)
            },
            comboBox
        }
            };

            // 3) הגדר את אפשרויות הדיאלוג
            var options = new SimpleContentDialogCreateOptions
            {
                Title = "Install JDK",
                Content = panel,
                PrimaryButtonText = "Install",
                CloseButtonText = "Cancel"
            };

            // 4) הצג דיאלוג דרך השירות (DialogHost כבר הוגדר ב־App.OnStartup)
            var result = await _dialogService.ShowSimpleDialogAsync(options);
            if (result != ContentDialogResult.Primary)
                return;

            // 5) התקן
            var selectedVersion = (int)comboBox.SelectedItem!;
            await InstallJdkAsync(selectedVersion);
        }


        private async Task InstallJdkAsync(int version)
        {
            IsInstallingJdk = true;
            InstallationStatus = "Starting JDK installation...";
            InstallationProgress = 0;

            var progress = new Progress<(string, int)>(report =>
            {
                InstallationStatus = report.Item1;
                InstallationProgress = report.Item2;
            });

            try
            {
                var helper = new JdkHelper();
                await helper.DownloadAndInstallJdkAsync(version, progress);
                InstallationStatus = "JDK installed successfully!";
                await Task.Delay(1000); // Show success message briefly
                RefreshJdks();
            }
            catch (Exception ex)
            {
                InstallationStatus = $"Installation failed: {ex.Message}";
                await Task.Delay(3000); // Show error message longer
            }
            finally
            {
                IsInstallingJdk = false;
                InstallationProgress = 0;
            }
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private void InitializeViewModel()
        {
            
            CurrentTheme = _themeService.LoadTheme();
            AppVersion = $"Modrix – v{GetAssemblyVersion()}";
            RefreshJdks();
            _isInitialized = true;
        }

        [RelayCommand]
        private void RefreshJdks()
        {
            var helper = new JdkHelper();
            var jdks = helper.GetInstalledJdks();
            InstalledJdks.Clear();
            foreach (var jdk in jdks)
            {
                InstalledJdks.Add(jdk);
            }
        }

        [RelayCommand]
        private void OpenJdkFolder(JdkHelper.JdkInfo jdk)
        {
            if (jdk != null && Directory.Exists(jdk.Path))
            {
                Process.Start("explorer.exe", jdk.Path);
            }
        }

        [RelayCommand]
        private async Task RemoveJdk(JdkHelper.JdkInfo jdk)
        {
            if (jdk != null && jdk.IsRemovable && Directory.Exists(jdk.Path))
            {
                try
                {
                    // Confirm deletion
                    var messageBox = new MessageBox
                    {
                        Title = "Confirm Removal",
                        Content = $"Are you sure you want to remove this JDK?\n\n{jdk.Path}",
                        PrimaryButtonText = "Delete",
                        CloseButtonText = "Cancel"
                    };

                    var result = await messageBox.ShowDialogAsync();

                    if (result != MessageBoxResult.Primary)
                        return;

                    Directory.Delete(jdk.Path, true);
                    RefreshJdks();
                }
                catch (Exception ex)
                {
                    var errorBox = new MessageBox
                    {
                        Title = "Error",
                        Content = $"Failed to remove JDK: {ex.Message}",
                        CloseButtonText = "OK"
                    };

                    await errorBox.ShowDialogAsync();
                }
            }
        }

        private string GetAssemblyVersion()
            => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? String.Empty;

        [RelayCommand]
        private void OnChangeTheme(string parameter)
        {
            var newTheme = parameter switch
            {
                "theme_light" => ApplicationTheme.Light,
                "theme_dark" => ApplicationTheme.Dark,
                "theme_highcontrast" => ApplicationTheme.HighContrast,
                _ => ApplicationTheme.Dark
            };

            if (CurrentTheme == newTheme)
                return;

            // 1) Apply
            ApplicationThemeManager.Apply(newTheme);
            CurrentTheme = newTheme;

            // 2) Save
            _themeService.SaveTheme(newTheme);
        }
    }
}
