using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModrixInstaller.Views.Pages;
using ModrixInstaller.ViewModels.Pages;
using System.Windows.Input;
using Wpf.Ui;
using System.Windows.Controls;

namespace ModrixInstaller.ViewModels.Windows;

public class MainWindowViewModel : ObservableObject
{
    private readonly IThemeService _themeService;
    private readonly LicensePage _licensePage;
    private readonly InstallerPage _installerPage;

    private string _applicationTitle = "Modrix Installer";
    public string ApplicationTitle
    {
        get => _applicationTitle;
        set => SetProperty(ref _applicationTitle, value);
    }

    private Page? _currentContent;
    public Page? CurrentContent
    {
        get => _currentContent;
        set => SetProperty(ref _currentContent, value);
    }

    private int _currentStepIndex;
    public int CurrentStepIndex
    {
        get => _currentStepIndex;
        set
        {
            if (SetProperty(ref _currentStepIndex, value))
            {
                CurrentContent = Steps[value];
                if (CurrentContent == _installerPage) _installerPage.ViewModel.InitializeIfNeeded();
                OnPropertyChanged(nameof(IsFirst));
                OnPropertyChanged(nameof(IsLast));
                UpdateEnabledStates();
                UpdateButtonProperties();
            }
        }
    }

    private bool _isDarkTheme;
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (SetProperty(ref _isDarkTheme, value))
            {
                // Apply immediately when bound ToggleButton changes
                _themeService.SetTheme(_isDarkTheme ? Wpf.Ui.Appearance.ApplicationTheme.Dark : Wpf.Ui.Appearance.ApplicationTheme.Light);
            }
        }
    }

    public IReadOnlyList<Page> Steps { get; }

    public bool IsFirst => CurrentStepIndex == 0;
    public bool IsLast => CurrentStepIndex == Steps.Count - 1;

    private bool _nextEnabled;
    public bool NextEnabled
    {
        get => _nextEnabled;
        set => SetProperty(ref _nextEnabled, value);
    }

    private bool _previousEnabled;
    public bool PreviousEnabled
    {
        get => _previousEnabled;
        set => SetProperty(ref _previousEnabled, value);
    }

    private string _nextButtonText = "Next";
    public string NextButtonText
    {
        get => _nextButtonText;
        set => SetProperty(ref _nextButtonText, value);
    }

    private string _nextButtonIcon = "ArrowRight24";
    public string NextButtonIcon
    {
        get => _nextButtonIcon;
        set => SetProperty(ref _nextButtonIcon, value);
    }

    private string _nextButtonToolTip = "Next";
    public string NextButtonToolTip
    {
        get => _nextButtonToolTip;
        set => SetProperty(ref _nextButtonToolTip, value);
    }

    public ICommand NextCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand ToggleThemeCommand { get; }

    public MainWindowViewModel(IThemeService themeService, LicensePage licensePage, InstallerPage installerPage)
    {
        _themeService = themeService;
        _licensePage = licensePage;
        _installerPage = installerPage;

        Steps = new List<Page> { _licensePage, _installerPage };

        _currentStepIndex = 0;
        _currentContent = Steps[0];
        _isDarkTheme = _themeService.GetTheme() == Wpf.Ui.Appearance.ApplicationTheme.Dark;

        NextCommand = new RelayCommand(Next);
        PreviousCommand = new RelayCommand(Previous);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);

        // Watch license acceptance
        if (_licensePage.ViewModel is LicenseViewModel vm)
        {
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LicenseViewModel.IsAccepted)) 
                {
                    UpdateEnabledStates();
                    UpdateButtonProperties();
                }
            };
        }

        // Watch installer state changes
        if (_installerPage.ViewModel is InstallerViewModel installerVm)
        {
            installerVm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(InstallerViewModel.IsAdministrator) ||
                    e.PropertyName == nameof(InstallerViewModel.SelectedRelease) ||
                    e.PropertyName == nameof(InstallerViewModel.InstallationPath) ||
                    e.PropertyName == nameof(InstallerViewModel.IsInstalling) ||
                    e.PropertyName == nameof(InstallerViewModel.InstallationCompleted))
                {
                    UpdateEnabledStates();
                    UpdateButtonProperties();
                }
            };
        }

        UpdateEnabledStates();
        UpdateButtonProperties();
    }

    private void UpdateEnabledStates()
    {
        PreviousEnabled = !IsFirst;
        var accepted = _licensePage.ViewModel.IsAccepted;
        NextEnabled = !IsLast && !(IsFirst && !accepted);
        
        // For installer page, enable button if we can install or need admin
        if (CurrentContent == _installerPage && IsLast)
        {
            var installerVm = _installerPage.ViewModel;
            // Enable if not installing and not completed, and has required info
            NextEnabled = !installerVm.IsInstalling && !installerVm.InstallationCompleted && 
                         (installerVm.SelectedRelease != null && !string.IsNullOrEmpty(installerVm.InstallationPath));
        }
    }

    private void UpdateButtonProperties()
    {
        if (CurrentContent == _installerPage && IsLast)
        {
            var installerVm = _installerPage.ViewModel;
            
            // If installation is completed, show completed state
            if (installerVm.InstallationCompleted)
            {
                NextButtonText = "Completed";
                NextButtonIcon = "CheckmarkCircle24";
                NextButtonToolTip = "Installation completed";
                return;
            }
            
            // If currently installing, show installing state
            if (installerVm.IsInstalling)
            {
                NextButtonText = "Installing...";
                NextButtonIcon = "ArrowDownload24";
                NextButtonToolTip = "Installation in progress";
                return;
            }
            
            if (!installerVm.IsAdministrator)
            {
                NextButtonText = "Restart as Admin";
                NextButtonIcon = "Shield24";
                NextButtonToolTip = "Restart with administrator privileges";
            }
            else
            {
                NextButtonText = "Install";
                NextButtonIcon = "ArrowDownload24";
                NextButtonToolTip = "Install Modrix";
            }
        }
        else
        {
            NextButtonText = "Next";
            NextButtonIcon = "ArrowRight24";
            NextButtonToolTip = "Next";
        }
    }

    private void Next()
    {
        if (!NextEnabled) return;
        
        // Handle special case for installer page
        if (CurrentContent == _installerPage && IsLast)
        {
            var installerVm = _installerPage.ViewModel;
            
            if (!installerVm.IsAdministrator)
            {
                // Request admin privileges
                installerVm.RequestAdministratorCommand.Execute(null);
            }
            else
            {
                // Start installation
                installerVm.InstallModrixCommand.Execute(null);
            }
            return;
        }
        
        CurrentStepIndex++;
    }

    private void Previous()
    {
        if (!PreviousEnabled) return;
        CurrentStepIndex--;
    }

    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme; // setter handles applying theme
    }
}
