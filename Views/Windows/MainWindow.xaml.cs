using System.Windows.Controls;
using Modrix.ViewModels.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class MainWindow : INavigationWindow
    {
        public MainWindowViewModel ViewModel { get; }

        public MainWindow(
            MainWindowViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService
        )
        {
            ViewModel = viewModel;
            DataContext = this;

            SystemThemeWatcher.Watch(this);

            InitializeComponent();
            SetPageService(navigationViewPageProvider);

            navigationService.SetNavigationControl(RootNavigation);
        }

        #region INavigationWindow methods

        public INavigationView GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) => RootNavigation.SetPageProviderService(navigationViewPageProvider);

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        #endregion INavigationWindow methods

        /// <summary>
        /// Raises the closed event.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Make sure that closing this window will begin the process of closing the application, Unless ProjectWorkspace.xaml is open.
            bool hasOpenWorkspace = false;
            foreach (Window window in Application.Current.Windows)
            {
                if (window is ProjectWorkspace workspaceWindow && workspaceWindow.IsLoaded)
                {
                    hasOpenWorkspace = true;
                    break;
                }
            }

            
            if (!hasOpenWorkspace)
            {
                Application.Current.Shutdown();
            }
        }

        public void ShowOfflineSnackbar()
        {
            if (RootNavigation.ContentOverlay is Grid overlay &&
                overlay.Children[0] is Wpf.Ui.Controls.SnackbarPresenter presenter)
            {
                var snackbar = new Wpf.Ui.Controls.Snackbar(presenter)
                {
                    Title = "Offline Mode",
                    Content = "You won't be able to create New Projects", // Changed from Message to Content
                    Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Warning24 }, // Proper icon creation
                    Appearance = Wpf.Ui.Controls.ControlAppearance.Caution, // Changed to ControlAppearance
                    Timeout = TimeSpan.FromSeconds(5) // Proper TimeSpan
                };

                snackbar.Show(); // Call Show on the snackbar itself
            }
        }



        INavigationView INavigationWindow.GetNavigation()
        {
            throw new NotImplementedException();
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }
    }
}
