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

        public void ReloadProject()
        {
            if (CurrentProject != null)
            {
                // Create a copy to force property changed notifications
                var project = new ModProjectData
                {
                    Location = CurrentProject.Location,
                    ModId = CurrentProject.ModId,
                    Name = CurrentProject.Name,
                    Package = CurrentProject.Package,
                    MinecraftVersion = CurrentProject.MinecraftVersion,
                    ModType = CurrentProject.ModType,
                    IconPath = CurrentProject.IconPath,
                    // Copy other properties as needed
                };

                CurrentProject = project;
            }
        }

        [ObservableProperty]
        private ModProjectData? _currentProject;

        public void LoadProject(ModProjectData project)
        {
            CurrentProject = project;

            // If you want to push individual values into their own properties:
            ModType = project.ModType;
            ApplicationTitle = $"Workspace — {project.Name}";
            Console.WriteLine($"Workspace — {project.Name}");

            // Trigger property changed for all properties
            OnPropertyChanged(nameof(CurrentProject));
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
                    Content = "IDE",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Code24 },
                    TargetPageType = typeof(Views.Pages.IDEPage)
                },
                new NavigationViewItem()
                {
                    Content = "Resources",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.DesignIdeas16 },
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
                    Content = "Community",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.PeopleCommunity24 },
                    Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() =>
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "https://discord.gg/3M58rQC2",
                            UseShellExecute = true
                        });
                    })
                },
                new NavigationViewItem()
                {
                    Content = "Console",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.WindowConsole20 },
                    TargetPageType = typeof(Views.Pages.ConsolePage)
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
