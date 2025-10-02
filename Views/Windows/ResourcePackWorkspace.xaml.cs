using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
                // Re-read pack to ensure overrides list is current
                var manager = new ResourcePackTemplateManager();
                var packData = manager.ReadResourcePack(ViewModel.CurrentPack.Location);

                var minecraftDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
                var resourcePacksDir = Path.Combine(minecraftDir, "resourcepacks");
                
                if (!Directory.Exists(resourcePacksDir))
                {
                    ShowSnackbar("Minecraft directory not found", "Could not find Minecraft installation", ControlAppearance.Danger);
                    return;
                }

                if (packData.Overrides == null || packData.Overrides.Count == 0)
                {
                    ShowSnackbar("No overrides to install", "Create overrides first", ControlAppearance.Secondary);
                    return;
                }

                var targetPath = Path.Combine(resourcePacksDir, packData.Name);
                if (Directory.Exists(targetPath))
                {
                    var result = await new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "Resource Pack Exists",
                        Content = $"A resource pack named '{packData.Name}' already exists. Replace it?",
                        PrimaryButtonText = "Replace",
                        CloseButtonText = "Cancel"
                    }.ShowDialogAsync();

                    if (result != Wpf.Ui.Controls.MessageBoxResult.Primary)
                        return;

                    Directory.Delete(targetPath, true);
                }

                var stagingDir = BuildStagingDirectory(packData);
                try
                {
                    CopyDirectory(stagingDir, targetPath);
                    ShowSnackbar("Resource pack installed", "Overrides deployed", ControlAppearance.Success);
                }
                finally
                {
                    TryDeleteDirectory(stagingDir);
                }
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
                    // Re-read pack metadata to get latest overrides
                    var manager = new ResourcePackTemplateManager();
                    var packData = manager.ReadResourcePack(ViewModel.CurrentPack.Location);

                    if (packData.Overrides == null || packData.Overrides.Count == 0)
                    {
                        ShowSnackbar("Nothing to export", "No overrides created yet", ControlAppearance.Secondary);
                        return;
                    }

                    ShowSnackbar("Exporting resource pack...", "Building archive");

                    if (File.Exists(dialog.FileName))
                        File.Delete(dialog.FileName);

                    var stagingDir = BuildStagingDirectory(packData);
                    try
                    {
                        ZipFile.CreateFromDirectory(stagingDir, dialog.FileName, CompressionLevel.Optimal, includeBaseDirectory: false);
                        ShowSnackbar("Export successful", Path.GetFileName(dialog.FileName), ControlAppearance.Success);
                    }
                    finally
                    {
                        TryDeleteDirectory(stagingDir);
                    }
                }
                catch (Exception ex)
                {
                    ShowSnackbar("Export failed", $"Error: {ex.Message}", ControlAppearance.Danger);
                }
            }
        }

        /// <summary>
        /// Builds a minimal staging directory for export / install containing only override files
        /// in their final asset paths plus pack.mcmeta / pack.png.
        /// </summary>
        private string BuildStagingDirectory(ResourcePackData pack)
        {
            var stagingRoot = Path.Combine(Path.GetTempPath(), "ModrixPack_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingRoot);

            // Copy metadata files
            var mcMetaSrc = Path.Combine(pack.Location, "pack.mcmeta");
            if (File.Exists(mcMetaSrc))
                File.Copy(mcMetaSrc, Path.Combine(stagingRoot, "pack.mcmeta"), true);

            var iconSrc = Path.Combine(pack.Location, "pack.png");
            if (File.Exists(iconSrc))
                File.Copy(iconSrc, Path.Combine(stagingRoot, "pack.png"), true);

            // Process overrides -> final location under stagingRoot
            foreach (var ov in pack.Overrides)
            {
                if (string.IsNullOrWhiteSpace(ov.OriginalPath) || string.IsNullOrWhiteSpace(ov.OverridePath))
                    continue;
                if (!File.Exists(ov.OverridePath))
                    continue;

                // OriginalPath already like: assets/minecraft/textures/... or assets/minecraft/lang/... 
                var normalized = ov.OriginalPath.Replace('/', Path.DirectorySeparatorChar);
                var destFull = Path.Combine(stagingRoot, normalized);
                Directory.CreateDirectory(Path.GetDirectoryName(destFull)!);
                File.Copy(ov.OverridePath, destFull, true);

                // IMPORTANT: Also copy .mcmeta file if it exists (for animated textures)
                var mcmetaSource = ov.OverridePath + ".mcmeta";
                if (File.Exists(mcmetaSource))
                {
                    var mcmetaDest = destFull + ".mcmeta";
                    File.Copy(mcmetaSource, mcmetaDest, true);
                }
            }

            return stagingRoot;
        }

        private void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { /* ignore */ }
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDir, file);
                var targetFile = Path.Combine(targetDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                File.Copy(file, targetFile, true);
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
    }
}