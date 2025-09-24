using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModrixInstaller.Services;
using System.ComponentModel;

namespace ModrixInstaller.ViewModels.Pages
{
    public partial class LicensePageViewModel : ObservableObject
    {
        private readonly LicenseService _licenseService;

        [ObservableProperty]
        private string _licenseText;

        [ObservableProperty]
        private bool _isLicenseAccepted;

        [ObservableProperty]
        private string _licenseTitle = "License Agreement";

        [ObservableProperty]
        private string _licenseSubtitle = "Please read the following license agreement carefully.";

        public LicensePageViewModel(LicenseService licenseService)
        {
            _licenseService = licenseService;
            _licenseText = _licenseService.GetLicenseText();
            _isLicenseAccepted = _licenseService.IsLicenseAccepted;
            
            // Subscribe to license service changes
            _licenseService.PropertyChanged += OnLicenseServicePropertyChanged;
        }

        private void OnLicenseServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LicenseService.IsLicenseAccepted))
            {
                IsLicenseAccepted = _licenseService.IsLicenseAccepted;
            }
        }

        [RelayCommand]
        private void AcceptLicense()
        {
            IsLicenseAccepted = true;
            _licenseService.AcceptLicense();
        }

        [RelayCommand]
        private void RejectLicense()
        {
            IsLicenseAccepted = false;
            _licenseService.DeclineLicense();
        }

        partial void OnIsLicenseAcceptedChanged(bool value)
        {
            if (value)
                _licenseService.AcceptLicense();
            else
                _licenseService.DeclineLicense();
        }
    }
}