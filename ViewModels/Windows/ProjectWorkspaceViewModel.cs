using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Modrix.Models;
using Wpf.Ui.Controls;

namespace Modrix.ViewModels.Windows
{
    public partial class ProjectWorkspaceViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "Project Workspace";

        [ObservableProperty]
        private ObservableCollection<object> _menuItems;

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems;

        [ObservableProperty]
        private string _modType;

        public ProjectWorkspaceViewModel()
        {
            InitializeMenuItems();
        }

        [ObservableProperty]
        private ModProjectData? _currentProject;

        public void LoadProject(ModProjectData project)
        {
            CurrentProject = project;

            // If you want to push individual values into their own properties:
            ModType = project.ModType;
            ApplicationTitle = $"Workspace — {project.Name}";
            // …and any other view-model fields you want to pre-fill…

            // Finally, navigate to your default sub-page:
            //Navigate(typeof(Views.Pages.WorkspacePage));
        }

        private void InitializeMenuItems()
        {
            MenuItems = new ObservableCollection<object>
            {
                new NavigationViewItem()
                {
                    Content = "Workspace",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Apps16 },
                    TargetPageType = typeof(Views.Pages.WorkspacePage)
                },
                new NavigationViewItem()
                {
                    Content = "Resources",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Box16 },
                    TargetPageType = typeof(Views.Pages.ResourcesPage)
                },
                new NavigationViewItem()
                {
                    Content = "Language Control",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.LocalLanguage16 },
                    TargetPageType = typeof(Views.Pages.LanguageControlPage)
                }
            };

            FooterMenuItems = new ObservableCollection<object>
            {
                new NavigationViewItem()
                {
                    Content = "Console",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.WindowConsole20 }
                },
                new NavigationViewItem()
                {
                    Content = "Donate",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.PersonHeart20 }
                }
            };
        }
    }
}
