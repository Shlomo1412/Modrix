using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModrixInstaller.Models;
using ModrixInstaller.Services;
using ModrixInstaller.Views.Pages;
using System.Collections.ObjectModel;
using System.Windows;
using Wpf.Ui.Controls;

namespace ModrixInstaller.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly ConfigurationService _configurationService;
        private readonly LicenseService _licenseService;

        private string _applicationTitle = "Modrix Installer";
        public string ApplicationTitle
        {
            get => _applicationTitle;
            set => SetProperty(ref _applicationTitle, value);
        }

        private ObservableCollection<InstallationStep> _installationSteps = new();
        public ObservableCollection<InstallationStep> InstallationSteps
        {
            get => _installationSteps;
            set => SetProperty(ref _installationSteps, value);
        }

        private InstallationStep? _currentStep;
        public InstallationStep? CurrentStep
        {
            get => _currentStep;
            set => SetProperty(ref _currentStep, value);
        }

        private int _currentStepIndex;
        public int CurrentStepIndex
        {
            get => _currentStepIndex;
            set => SetProperty(ref _currentStepIndex, value);
        }

        private bool _canGoNext = true;
        public bool CanGoNext
        {
            get => _canGoNext;
            set => SetProperty(ref _canGoNext, value);
        }

        private bool _canGoBack = false;
        public bool CanGoBack
        {
            get => _canGoBack;
            set => SetProperty(ref _canGoBack, value);
        }

        private bool _canCancel = true;
        public bool CanCancel
        {
            get => _canCancel;
            set => SetProperty(ref _canCancel, value);
        }

        private string _nextButtonText = "Next";
        public string NextButtonText
        {
            get => _nextButtonText;
            set => SetProperty(ref _nextButtonText, value);
        }

        private SymbolRegular _nextButtonIcon = SymbolRegular.ChevronRight24;
        public SymbolRegular NextButtonIcon
        {
            get => _nextButtonIcon;
            set => SetProperty(ref _nextButtonIcon, value);
        }

        public MainWindowViewModel(ConfigurationService configurationService, LicenseService licenseService)
        {
            _configurationService = configurationService;
            _licenseService = licenseService;

            InitializeInstallationSteps();
            CurrentStep = InstallationSteps.FirstOrDefault();
            CurrentStepIndex = 0;
            UpdateNavigationState();
        }

        private void InitializeInstallationSteps()
        {
            InstallationSteps = new ObservableCollection<InstallationStep>
            {
                new InstallationStep
                {
                    Title = "Welcome",
                    Description = "Welcome to Modrix Setup",
                    Icon = SymbolRegular.Home24,
                    PageType = typeof(WelcomePage),
                    IsActive = true
                },
                new InstallationStep
                {
                    Title = "License",
                    Description = "License Agreement",
                    Icon = SymbolRegular.Document24,
                    PageType = typeof(LicensePage)
                },
                new InstallationStep
                {
                    Title = "Options",
                    Description = "Installation Options",
                    Icon = SymbolRegular.Settings24,
                    PageType = typeof(InstallationOptionsPage)
                },
                new InstallationStep
                {
                    Title = "Install",
                    Description = "Installing Modrix",
                    Icon = SymbolRegular.ArrowDownload24,
                    PageType = typeof(InstallationProgressPage)
                },
                new InstallationStep
                {
                    Title = "Complete",
                    Description = "Installation Complete",
                    Icon = SymbolRegular.CheckmarkCircle24,
                    PageType = typeof(CompletePage)
                }
            };
        }

        [RelayCommand]
        private void GoNext()
        {
            if (CurrentStepIndex < InstallationSteps.Count - 1)
            {
                // Validate current step before proceeding
                if (ValidateCurrentStep())
                {
                    CurrentStep!.IsCompleted = true;
                    CurrentStep.IsActive = false;
                    
                    CurrentStepIndex++;
                    CurrentStep = InstallationSteps[CurrentStepIndex];
                    CurrentStep.IsActive = true;
                    
                    UpdateNavigationState();
                }
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            if (CurrentStepIndex > 0)
            {
                CurrentStep!.IsActive = false;
                
                CurrentStepIndex--;
                CurrentStep = InstallationSteps[CurrentStepIndex];
                CurrentStep.IsActive = true;
                CurrentStep.IsCompleted = false;
                
                UpdateNavigationState();
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            // Handle cancellation
            Application.Current.Shutdown();
        }

        private bool ValidateCurrentStep()
        {
            return CurrentStep?.Title switch
            {
                "License" => _licenseService.IsLicenseAccepted,
                "Options" => _configurationService.IsValidInstallPath(_configurationService.Configuration.InstallPath),
                _ => true
            };
        }

        private void UpdateNavigationState()
        {
            CanGoBack = CurrentStepIndex > 0 && CurrentStepIndex < 3; // Can't go back during install or from complete
            CanGoNext = CurrentStepIndex < InstallationSteps.Count - 1;
            CanCancel = CurrentStepIndex < 3; // Can't cancel during install

            NextButtonText = CurrentStepIndex switch
            {
                0 => "Next",
                1 => "I Agree",
                2 => "Install",
                3 => "Installing...",
                4 => "Finish",
                _ => "Next"
            };

            NextButtonIcon = CurrentStepIndex switch
            {
                2 => SymbolRegular.ArrowDownload24,
                4 => SymbolRegular.Checkmark24,
                _ => SymbolRegular.ChevronRight24
            };

            // Disable next button during installation
            if (CurrentStepIndex == 3)
            {
                CanGoNext = false; // Will be enabled when installation completes
            }
        }

        public void SetInstallationComplete()
        {
            CurrentStep!.IsCompleted = true;
            CanGoNext = true;
            UpdateNavigationState();
        }

        public void UpdateStepValidation(string stepTitle, bool isValid)
        {
            var step = InstallationSteps.FirstOrDefault(s => s.Title == stepTitle);
            if (step != null)
            {
                step.IsEnabled = isValid;
            }
            
            if (CurrentStep?.Title == stepTitle)
            {
                CanGoNext = isValid && CurrentStepIndex < InstallationSteps.Count - 1;
            }
        }
    }
}