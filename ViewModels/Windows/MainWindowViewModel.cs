using System.Collections.ObjectModel;
using System.Windows;
using Wpf.Ui.Controls;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Modrix.Views.Windows;

namespace Modrix.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "Modrix";

        private IRelayCommand _newProjectCommand;
        private IRelayCommand _showDiscordDialogCommand;

        public MainWindowViewModel()
        {
            _newProjectCommand = new RelayCommand(OpenNewProject);
            _showDiscordDialogCommand = new RelayCommand(ShowDiscordDialog);
            InitializeMenuItems();
            InitializeFooterMenuItems();
        }

        private void InitializeMenuItems()
        {
            MenuItems = new ObservableCollection<object>
            {
                new NavigationViewItem()
                {
                    Content = "Home",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
                    TargetPageType = typeof(Views.Pages.DashboardPage)
                },
                new NavigationViewItem()
                {
                    Content = "News",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.News24 },
                    TargetPageType = typeof(Views.Pages.NewsPage)
                },
                new NavigationViewItem()
                {
                    Content = "New Project",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Add24 },
                    Command = _newProjectCommand
                }
            };
        }

        [ObservableProperty]
        private ObservableCollection<object> _menuItems;

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems;

        private void InitializeFooterMenuItems()
        {
            FooterMenuItems = new ObservableCollection<object>
            {
                new NavigationViewItem()
                {
                    Content = "Community",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.PeopleCommunity24 },
                    Command = new RelayCommand(ShowDiscordDialog)
                },
                new NavigationViewItem()
                {
                    Content = "Settings",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                    TargetPageType = typeof(Views.Pages.SettingsPage)
                }
            };
        }

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems = new()
        {
            new MenuItem { Header = "Home", Tag = "tray_home" }
        };

        private void OpenNewProject()
        {
            var newProjectWindow = new Views.Windows.NewProject();
            newProjectWindow.Owner = Application.Current.MainWindow;
            newProjectWindow.ShowDialog();
        }

        private void ShowDiscordDialog()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new JoinDiscordDialog();
                dialog.Owner = Application.Current.MainWindow;
                dialog.ShowDialog();
            });
        }
    }
}
