using Modrix.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;
using System.Windows;
using Modrix.Models;
using Modrix.Views.Controls;
using Modrix.Views.Windows;
using System;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Modrix.ViewModels.Windows;

namespace Modrix.Views.Pages
{
    public partial class DashboardPage : INavigableView<DashboardViewModel>
    {
        private readonly IServiceProvider _serviceProvider;
        public DashboardViewModel ViewModel { get; }

        public DashboardPage(DashboardViewModel viewModel, IServiceProvider serviceProvider)
        {
            ViewModel = viewModel;
            _serviceProvider = serviceProvider;
            DataContext = this;

            InitializeComponent();
            ViewModel.RefreshProjectsCommand.Execute(null);
        }

        private async void ProjectCard_DeleteClicked(object sender, RoutedEventArgs e)
        {
            if (sender is ProjectCard card && card.ProjectData is ModProjectData project)
            {
                var messageBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Delete Project",
                    Content = $"Are you sure you want to delete {project.Name}?",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel"
                };

                var result = await messageBox.ShowDialogAsync();
                if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
                {
                    await ViewModel.DeleteProject(project);
                    
                    ViewModel.RefreshProjects();
                    ViewModel.RefreshProjectsCommand.Execute(null);
                }
            }
        }

        private void ProjectCard_EditClicked(object sender, RoutedEventArgs e)
        {
            if (sender is ProjectCard card && card.ProjectData is ModProjectData project)
            {
                // Try to find an existing window for this project
                var existingWindow = Application.Current.Windows.OfType<ProjectWorkspace>()
                    .FirstOrDefault(w => w.ViewModel.CurrentProject?.Location == project.Location);

                if (existingWindow != null)
                {
                    existingWindow.Activate();
                    return;
                }

                // Get required services for ProjectWorkspace
                var viewModel = _serviceProvider.GetRequiredService<ProjectWorkspaceViewModel>();
                var navigationViewPageProvider = _serviceProvider.GetRequiredService<INavigationViewPageProvider>();
                var navigationService = _serviceProvider.GetRequiredService<INavigationService>();

                // Create a new workspace window with fresh dependencies
                var workspaceWindow = new ProjectWorkspace(
                    viewModel,
                    navigationViewPageProvider,
                    navigationService
                );

                // Load the project into the workspace VM
                workspaceWindow.LoadProject(project);

                // Show the new workspace window
                workspaceWindow.Show();
                workspaceWindow.Activate();

                ViewModel.RefreshProjectsCommand.Execute(null);
            }
        }

        private void ProjectCard_OpenFolderClicked(object sender, RoutedEventArgs e)
        {
            if (sender is ProjectCard card && card.ProjectData is ModProjectData project)
            {
                ViewModel.OpenProjectFolderCommand.Execute(project);
            }
        }
    }
}
