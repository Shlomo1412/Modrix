using ModrixInstaller.ViewModels.Pages;
using System.Windows.Controls;

namespace ModrixInstaller.Views.Pages;

public partial class LicensePage : Page
{
    public LicenseViewModel ViewModel { get; }
    public LicensePage(LicenseViewModel vm)
    {
        ViewModel = vm;
        DataContext = this;
        InitializeComponent();
    }
}