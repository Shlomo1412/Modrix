using ModrixInstaller.ViewModels.Pages;
using System.Windows.Controls;

namespace ModrixInstaller.Views.Pages;

public partial class InstallerPage : Page
{
    public InstallerViewModel ViewModel { get; }

    public InstallerPage(InstallerViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        
        InitializeComponent();
    }
}