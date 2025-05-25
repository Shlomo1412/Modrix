
using Modrix.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;
using System.Windows;
using Modrix.Models;
using Modrix.Views.Controls;
using Modrix.Views.Windows;
using System;
using Microsoft.Extensions.DependencyInjection;

namespace Modrix.Views.Pages
{
    public partial class DashboardPage : INavigableView<DashboardViewModel>
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage(DashboardViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
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
                }
            }
        }

        private void ProjectCard_EditClicked(object sender, RoutedEventArgs e)
        {
            if (sender is ProjectCard card && card.ProjectData is ModProjectData project)
            {
                
                var workspaceWindow = App.Services.GetService<ProjectWorkspace>();
                if (workspaceWindow != null)
                {
                    
                    //workspaceWindow.ViewModel.LoadProject(project);
                    workspaceWindow.Show();
                }
            }
        }



    }
}
