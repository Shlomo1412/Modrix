using System.Collections.ObjectModel;
using System.Windows;
using Wpf.Ui.Controls;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Modrix.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "Modrix";

        private IRelayCommand _newProjectCommand;

        public MainWindowViewModel()
        {
            _newProjectCommand = new RelayCommand(OpenNewProject);
            InitializeMenuItems();
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
                    Content = "Data",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.DataHistogram24 },
                    TargetPageType = typeof(Views.Pages.DataPage)
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
        private ObservableCollection<object> _footerMenuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Community",
                Icon = new SymbolIcon { Symbol = SymbolRegular.PeopleCommunity24 },
                Command = new RelayCommand(() =>
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
                Content = "Settings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage)
            }
        };

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
    }
}
