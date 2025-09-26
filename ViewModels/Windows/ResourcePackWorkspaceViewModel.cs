using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modrix.Services;
using Modrix.Views.Windows;
using System.Windows;
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
        private ResourcePackData? _currentPack;

        public ResourcePackWorkspaceViewModel()
        {
            System.Diagnostics.Debug.WriteLine("ResourcePackWorkspaceViewModel: Constructor called");
            InitializeMenuItems();
            InitializeFooterMenuItems();
            System.Diagnostics.Debug.WriteLine($"ResourcePackWorkspaceViewModel: MenuItems count = {MenuItems?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine($"ResourcePackWorkspaceViewModel: FooterMenuItems count = {FooterMenuItems?.Count ?? 0}");
        }

        public void LoadPack(ResourcePackData pack)
        {
            CurrentPack = pack;
            ApplicationTitle = $"Resource Pack — {pack.Name}";

            // Trigger property changed for all properties
            OnPropertyChanged(nameof(CurrentPack));
        }

        private void InitializeMenuItems()
        {
            MenuItems = new ObservableCollection<object>
            {
                new NavigationViewItem()
                {
                    Content = "Overrides",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.DocumentEdit24 },
                    TargetPageType = typeof(Views.Pages.ResourcePack.OverridesPage),
                    ToolTip = "Manage your texture and translation overrides"
                },
                new NavigationViewItem()
                {
                    Content = "Textures",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Image24 },
                    TargetPageType = typeof(Views.Pages.ResourcePack.TexturesPage),
                    ToolTip = "Browse and edit Minecraft textures"
                },
                new NavigationViewItem()
                {
                    Content = "Translations",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.LocalLanguage24 },
                    TargetPageType = typeof(Views.Pages.ResourcePack.TranslationsPage),
                    ToolTip = "Edit language and translation files"
                },
                new NavigationViewItem()
                {
                    Content = "Models",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Cube24 },
                    TargetPageType = typeof(Views.Pages.ResourcePack.ModelsPage),
                    ToolTip = "Browse and edit Minecraft models"
                },
                new NavigationViewItem()
                {
                    Content = "Properties",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                    TargetPageType = typeof(Views.Pages.ResourcePack.PropertiesPage),
                    ToolTip = "Edit resource pack properties and metadata"
                }
            };
            
            System.Diagnostics.Debug.WriteLine($"ResourcePackWorkspaceViewModel: Initialized {MenuItems.Count} menu items");
        }

        private void InitializeFooterMenuItems()
        {
            FooterMenuItems = new ObservableCollection<object>
            {
                new NavigationViewItem()
                {
                    Content = "Wiki",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.BookInformation24 },
                    TargetPageType = typeof(Views.Pages.WikiPage),
                    ToolTip = "Access the Modrix wiki and documentation"
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