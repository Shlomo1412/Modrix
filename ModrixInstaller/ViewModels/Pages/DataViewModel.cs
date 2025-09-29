using System.Diagnostics;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace ModrixInstaller.ViewModels.Pages
{
    public partial class DataViewModel : ObservableObject, INavigationAware
    {
        private readonly ISnackbarService _snackbarService;
        private bool _isInitialized = false;

        public DataViewModel(ISnackbarService snackbarService)
        {
            _snackbarService = snackbarService;
        }

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();

            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        [RelayCommand]
        private void OpenRepository()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/Shlomo1412/Modrix",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _snackbarService.Show("Error", $"Failed to open repository: {ex.Message}", ControlAppearance.Danger, null, TimeSpan.FromSeconds(3));
            }
        }

        [RelayCommand]
        private void OpenIssues()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/Shlomo1412/Modrix/issues",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _snackbarService.Show("Error", $"Failed to open issues page: {ex.Message}", ControlAppearance.Danger, null, TimeSpan.FromSeconds(3));
            }
        }

        private void InitializeViewModel()
        {
            _isInitialized = true;
        }
    }
}
