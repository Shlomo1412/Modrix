using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modrix.Models;
using Modrix.Views.Windows;
using Wpf.Ui.Controls;

namespace Modrix.ViewModels.Windows
{
    public partial class ResourcePackWorkspaceViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "Resource Pack Workspace";

        [ObservableProperty]
        private ObservableCollection<object> _menuItems;

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems;

        [ObservableProperty]
        private ModProjectData? _currentProject;

        public ResourcePackWorkspaceViewModel()
        {
            InitializeMenuItems();
            InitializeFooterMenuItems();
        }

        public void LoadProject(ModProjectData project)
        {
            CurrentProject = project;
            ApplicationTitle = $"Resource Pack Workspace — {project.Name}";
            
            // Trigger property changed for all properties
            OnPropertyChanged(nameof(CurrentProject));
        }

        public void RefreshProject()
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
                    Description = CurrentProject.Description,
                    Authors = CurrentProject.Authors,
                    License = CurrentProject.License,
                    Version = CurrentProject.Version,
                    IncludeReadme = CurrentProject.IncludeReadme
                };

                CurrentProject = project;
            }
        }

        private void InitializeMenuItems()
        {
            MenuItems = new ObservableCollection<object>
            {
                new NavigationViewItem()
                {
                    Content = "Overrides",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.LayerDiagonal20 },
                    TargetPageType = typeof(Views.Pages.OverridesPage),
                    ToolTip = "View and manage all resource pack overrides"
                },
                new NavigationViewItem()
                {
                    Content = "Textures",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Image20 },
                    TargetPageType = typeof(Views.Pages.TexturesPage),
                    ToolTip = "Browse Minecraft textures and create overrides"
                },
                new NavigationViewItem()
                {
                    Content = "Translations",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.LocalLanguage20 },
                    TargetPageType = typeof(Views.Pages.TranslationsPage),
                    ToolTip = "Edit language files and translation overrides"
                },
                new NavigationViewItem()
                {
                    Content = "Properties",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Settings20 },
                    TargetPageType = typeof(Views.Pages.PropertiesPage),
                    ToolTip = "Configure resource pack properties and metadata"
                }
            };
        }

        private void InitializeFooterMenuItems()
        {
            FooterMenuItems = new ObservableCollection<object>
            {
                new NavigationViewItem()
                {
                    Content = "Wiki",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.BookInformation20 },
                    TargetPageType = typeof(Views.Pages.WikiPage),
                    ToolTip = "Access the resource pack creation wiki"
                },
                new NavigationViewItem()
                {
                    Content = "Community",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.PeopleCommunity24 },
                    Command = new RelayCommand(ShowDiscordDialog),
                    ToolTip = "Join our community on Discord"
                },
                new NavigationViewItem()
                {
                    Content = "Donation",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.PersonHeart20 },
                    Command = new RelayCommand(ShowDonateDialog),
                    ToolTip = "Support us with a donation"
                },
                new NavigationViewItem()
                {
                    Content = "Settings",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                    TargetPageType = typeof(Views.Pages.SettingsPage),
                    ToolTip = "Adjust application settings"
                }
            };
        }

        private void ShowDiscordDialog()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new JoinDiscordDialog();
                dialog.Owner = Application.Current.Windows.Count > 0 ? Application.Current.Windows[0] : null;
                dialog.ShowDialog();
            });
        }

        private void ShowDonateDialog()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new DonateDialog();
                dialog.Owner = Application.Current.Windows.Count > 0 ? Application.Current.Windows[0] : null;
                dialog.ShowDialog();
            });
        }
    }
}