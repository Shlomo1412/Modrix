// Views/Pages/DashboardPage.xaml.cs
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
            DataContext = this; // זה היה הבאג - אנחנו רוצים שה-DataContext יהיה this

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
                    // רענון הרשימה אחרי המחיקה
                    //ViewModel.RefreshProjects();
                }
            }
        }

        private void ProjectCard_EditClicked(object sender, RoutedEventArgs e)
        {
            if (sender is ProjectCard card && card.ProjectData is ModProjectData project)
            {
                // יצירת חלון חדש של ProjectWorkspace עם הפרויקט הנבחר
                var workspaceWindow = App.Services.GetService<ProjectWorkspace>();
                if (workspaceWindow != null)
                {
                    // העברת המידע של הפרויקט ל-ViewModel
                    //workspaceWindow.ViewModel.LoadProject(project);
                    workspaceWindow.Show();
                }
            }
        }



    }
}
