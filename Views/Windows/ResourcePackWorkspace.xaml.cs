using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using Modrix.Services;
using Modrix.ViewModels.Windows;
using Modrix.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class ResourcePackWorkspace : FluentWindow, INavigationWindow
    {
        public ResourcePackWorkspaceViewModel ViewModel { get; }
        private SnackbarPresenter _snackbarPresenter;
        private bool _onOpenHandled = false;

        public ResourcePackWorkspace(
            ResourcePackWorkspaceViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService
        )
        {
            System.Diagnostics.Debug.WriteLine("ResourcePackWorkspace: Constructor called");
            ViewModel = viewModel;
            DataContext = ViewModel;  // Fix: Set DataContext to ViewModel, not this

            System.Diagnostics.Debug.WriteLine($"ResourcePackWorkspace: ViewModel set, MenuItems = {ViewModel.MenuItems?.Count ?? 0}");

            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("ResourcePackWorkspace: InitializeComponent completed");
            
            SetPageService(navigationViewPageProvider);
            System.Diagnostics.Debug.WriteLine("ResourcePackWorkspace: SetPageService completed");

            navigationService.SetNavigationControl(RootNavigation);
            System.Diagnostics.Debug.WriteLine("ResourcePackWorkspace: SetNavigationControl completed");

            Loaded += ResourcePackWorkspace_Loaded;
        }

        private void ResourcePackWorkspace_Loaded(object sender, RoutedEventArgs e)
        {
            _snackbarPresenter = this.SnackbarPresenter;
            HandleOnOpenSettings();
        }

        private void HandleOnOpenSettings()
        {
            if (_onOpenHandled) return;
            _onOpenHandled = true;
            
            // Get IdeSettings from SettingsViewModel singleton
            var settingsVm = Modrix.App.Services.GetService(typeof(Modrix.ViewModels.Pages.SettingsViewModel)) as Modrix.ViewModels.Pages.SettingsViewModel;
            if (settingsVm == null) return;
            var ideSettings = settingsVm.IdeSettings;
            
            // Close MainWindow if setting is enabled
            if (ideSettings.CloseMainWindowOnOpen)
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is MainWindow mainWin && !ReferenceEquals(mainWin, this))
                    {
                        mainWin.Close();
                        break;
                    }
                }
            }
            
            // Navigate to the Overrides page by default for ResourcePacks
            NavigateToTab("Overrides");
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.CurrentPack != null)
            {
                // Refresh the entire pack
                RefreshPack(ViewModel.CurrentPack);
            }
            else
            {
                ShowSnackbar("No resource pack loaded", "Please open a resource pack first");
            }
        }

        private void RefreshPack(ResourcePackData pack)
        {
            // Reload pack
            ViewModel.LoadPack(pack);

            // Navigate to overrides page
            RootNavigation.Navigate(typeof(Views.Pages.ResourcePack.OverridesPage));

            ShowSnackbar("Resource pack refreshed", "All overrides reloaded", ControlAppearance.Success);
        }

        private void OpenPackFolder_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.CurrentPack != null &&
                Directory.Exists(ViewModel.CurrentPack.Location))
            {
                Process.Start("explorer.exe", ViewModel.CurrentPack.Location);
            }
            else
            {
                ShowSnackbar("Pack directory not found", "Error", ControlAppearance.Danger);
            }
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.CurrentPack == null)
            {
                ShowSnackbar("No resource pack loaded", "Please open a resource pack first");
                return;
            }

            try
            {
                var minecraftDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
                var resourcePacksDir = Path.Combine(minecraftDir, "resourcepacks");
                
                if (!Directory.Exists(resourcePacksDir))
                {
                    ShowSnackbar("Minecraft directory not found", "Could not find Minecraft installation", ControlAppearance.Danger);
                    return;
                }

                var targetPath = Path.Combine(resourcePacksDir, Path.GetFileName(ViewModel.CurrentPack.Location));
                
                if (Directory.Exists(targetPath))
                {
                    var result = await new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "Resource Pack Exists",
                        Content = $"A resource pack with the same name already exists in Minecraft. Do you want to replace it?",
                        PrimaryButtonText = "Replace",
                        CloseButtonText = "Cancel"
                    }.ShowDialogAsync();

                    if (result != Wpf.Ui.Controls.MessageBoxResult.Primary)
                        return;

                    Directory.Delete(targetPath, true);
                }

                // Copy the resource pack
                CopyDirectory(ViewModel.CurrentPack.Location, targetPath);
                
                ShowSnackbar("Resource pack installed", "Successfully installed to Minecraft", ControlAppearance.Success);
            }
            catch (Exception ex)
            {
                ShowSnackbar("Installation failed", $"Error: {ex.Message}", ControlAppearance.Danger);
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.CurrentPack == null) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Resource Pack",
                Filter = "ZIP Archive|*.zip",
                FileName = $"{ViewModel.CurrentPack.Name}.zip"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ShowSnackbar("Exporting resource pack...", "Please wait");
                    
                    // Create ZIP file from resource pack directory
                    if (File.Exists(dialog.FileName))
                        File.Delete(dialog.FileName);
                        
                    ZipFile.CreateFromDirectory(ViewModel.CurrentPack.Location, dialog.FileName);
                    
                    ShowSnackbar("Export successful", $"Resource pack exported to {Path.GetFileName(dialog.FileName)}", ControlAppearance.Success);
                }
                catch (Exception ex)
                {
                    ShowSnackbar("Export failed", $"Error: {ex.Message}", ControlAppearance.Danger);
                }
            }
        }

        public void LoadPack(ResourcePackData pack) => ViewModel.LoadPack(pack);

        public INavigationView GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(INavigationViewPageProvider provider)
        {
            RootNavigation.SetPageProviderService(provider);
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            // Not needed
        }

        private void ShowSnackbar(string message, string title,
                                 ControlAppearance appearance = ControlAppearance.Info)
        {
            if (_snackbarPresenter == null) return;

            var snackbar = new Snackbar(_snackbarPresenter)
            {
                Title = title,
                Content = message,
                Timeout = TimeSpan.FromSeconds(3),
                Appearance = appearance
            };
            snackbar.Show();
        }

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        public void NavigateToTab(string tabName)
        {
            // Map tab names to page types
            Type? pageType = tabName switch
            {
                "Overrides" => typeof(Modrix.Views.Pages.ResourcePack.OverridesPage),
                "Textures" => typeof(Modrix.Views.Pages.ResourcePack.TexturesPage),
                "Translations" => typeof(Modrix.Views.Pages.ResourcePack.TranslationsPage),
                "Properties" => typeof(Modrix.Views.Pages.ResourcePack.PropertiesPage),
                _ => typeof(Modrix.Views.Pages.ResourcePack.OverridesPage)
            };
            
            System.Diagnostics.Debug.WriteLine($"ResourcePackWorkspace: Navigating to {pageType?.Name}");
            var success = RootNavigation.Navigate(pageType);
            System.Diagnostics.Debug.WriteLine($"ResourcePackWorkspace: Navigation success = {success}");
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDir, file);
                var targetFile = Path.Combine(targetDir, relativePath);
                
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                File.Copy(file, targetFile, true);
            }
        }
    }
}