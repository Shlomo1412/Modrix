using Modrix.Models;
using Modrix.ViewModels.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class ProjectWorkspace : FluentWindow, INavigationWindow
    {
        public ProjectWorkspaceViewModel ViewModel { get; }

        public ProjectWorkspace(
            ProjectWorkspaceViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService
        )
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
            SetPageService(navigationViewPageProvider);

            navigationService.SetNavigationControl(RootNavigation);
        }

        public void LoadProject(ModProjectData project)
        => ViewModel.LoadProject(project);

        public INavigationView GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(INavigationViewPageProvider provider)
        {
            RootNavigation.SetPageProviderService(provider);
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            
        }

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();
    }
}
