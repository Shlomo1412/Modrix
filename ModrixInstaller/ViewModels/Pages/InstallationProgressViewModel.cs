using CommunityToolkit.Mvvm.ComponentModel;
using ModrixInstaller.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace ModrixInstaller.ViewModels.Pages
{
    public partial class InstallationProgressViewModel : ObservableObject
    {
        private readonly InstallationService _installationService;
        private readonly ConfigurationService _configurationService;

        [ObservableProperty]
        private int _progressPercentage;

        [ObservableProperty]
        private string _currentStatus = "Preparing installation...";

        [ObservableProperty]
        private string _currentStep = "Initializing...";

        [ObservableProperty]
        private bool _isInstalling;

        [ObservableProperty]
        private bool _isCompleted;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> _installationLog;

        [ObservableProperty]
        private string _elapsedTime = "00:00";

        private DateTime _startTime;
        private System.Timers.Timer? _timer;

        public InstallationProgressViewModel(InstallationService installationService, ConfigurationService configurationService)
        {
            _installationService = installationService;
            _configurationService = configurationService;
            _installationLog = new ObservableCollection<string>();

            _installationService.ProgressChanged += OnProgressChanged;
            _installationService.StatusChanged += OnStatusChanged;
        }

        public async Task StartInstallationAsync()
        {
            if (IsInstalling) return;

            IsInstalling = true;
            IsCompleted = false;
            HasError = false;
            ErrorMessage = string.Empty;
            ProgressPercentage = 0;
            InstallationLog.Clear();

            _startTime = DateTime.Now;
            StartTimer();

            LogMessage("Installation started");

            try
            {
                var success = await _installationService.InstallAsync(_configurationService.Configuration);
                
                if (success)
                {
                    IsCompleted = true;
                    LogMessage("Installation completed successfully!");
                }
                else
                {
                    HasError = true;
                    ErrorMessage = "Installation failed. Please check the log for details.";
                    LogMessage("Installation failed");
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Installation error: {ex.Message}";
                LogMessage($"Error: {ex.Message}");
            }
            finally
            {
                IsInstalling = false;
                StopTimer();
            }
        }

        private void OnProgressChanged(object? sender, InstallationProgressEventArgs e)
        {
            ProgressPercentage = e.Percentage;
            CurrentStep = e.Message;
            LogMessage($"{e.Percentage}% - {e.Message}");
        }

        private void OnStatusChanged(object? sender, string status)
        {
            CurrentStatus = status;
            LogMessage(status);
        }

        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Application.Current.Dispatcher.Invoke(() =>
            {
                InstallationLog.Add($"[{timestamp}] {message}");
            });
        }

        private void StartTimer()
        {
            _timer = new System.Timers.Timer(1000); // Update every second
            _timer.Elapsed += (s, e) =>
            {
                var elapsed = DateTime.Now - _startTime;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ElapsedTime = elapsed.ToString(@"mm\:ss");
                });
            };
            _timer.Start();
        }

        private void StopTimer()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        }

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            // Notify main window when installation is complete
            if (e.PropertyName == nameof(IsCompleted) && IsCompleted)
            {
                // This will be handled by the main window to enable the Next button
            }
        }
    }
}