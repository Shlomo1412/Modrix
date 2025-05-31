using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Modrix.Services;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace Modrix.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private readonly IThemeService _themeService;
        private bool _isInitialized = false;

        [ObservableProperty]
        private string _appVersion = String.Empty;

        [ObservableProperty]
        private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

        [ObservableProperty]
        private ObservableCollection<JdkHelper.JdkInfo> _installedJdks = new();


        public SettingsViewModel(IThemeService themeService)
        {
            _themeService = themeService;
        }

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();

            return Task.CompletedTask;
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
        private void RemoveJdk(JdkHelper.JdkInfo jdk)
        {
            if (jdk != null && jdk.IsRemovable && Directory.Exists(jdk.Path))
            {
                try
                {
                    Directory.Delete(jdk.Path, true);
                    RefreshJdks();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to remove JDK: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
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
