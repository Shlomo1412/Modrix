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
            _isInitialized = true;
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
