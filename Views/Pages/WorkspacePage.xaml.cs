using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Modrix.ModElements;
using Modrix.ModElements.Generators;

namespace Modrix.Views.Pages
{
    /// <summary>
    /// Interaction logic for WorkspacePage.xaml
    /// </summary>
    public partial class WorkspacePage : Page, INotifyPropertyChanged
    {
        private string _projectPath;
        private ModElementManager _elementManager;
        private ObservableCollection<ModElementData> _modElements = new();
        private ICollectionView _modElementsView;
        private string _searchText = "";
        private string _selectedFilterOption;

        public ObservableCollection<IModElementGenerator> AvailableModElements { get; set; } = new();
        public bool IsAddElementFlyoutOpen { get; set; }
        public ICommand OpenAddElementFlyoutCommand { get; }
        public ICommand AddModElementCommand { get; }
        public ICommand EditModElementCommand { get; }
        public ICommand DeleteModElementCommand { get; }

        public string SearchText 
        { 
            get => _searchText; 
            set 
            { 
                _searchText = value; 
                OnPropertyChanged(nameof(SearchText));
                _modElementsView?.Refresh();
            } 
        }

        public ObservableCollection<ModElementData> ModElements => _modElements;

        public IEnumerable<string> FilterOptions { get; } = new List<string> 
        { 
            "All Elements",
            "Items Only",
            "Blocks Only",
            "Entities Only" 
        };

        public string SelectedFilterOption 
        { 
            get => _selectedFilterOption; 
            set 
            { 
                _selectedFilterOption = value; 
                OnPropertyChanged(nameof(SelectedFilterOption));
                _modElementsView?.Refresh();
            } 
        }

        public WorkspacePage()
        {
            InitializeComponent();
            DataContext = this;

            _selectedFilterOption = FilterOptions.First();
            
            OpenAddElementFlyoutCommand = new RelayCommand(OpenAddElementFlyout);
            AddModElementCommand = new RelayCommand<IModElementGenerator>(AddModElement);
            EditModElementCommand = new RelayCommand<ModElementData>(EditModElement);
            DeleteModElementCommand = new RelayCommand<ModElementData>(DeleteModElement);
            
            // Register available mod elements
            AvailableModElements.Clear();
            AvailableModElements.Add(new ItemModElementGenerator());

            // Get project path
            var workspace = Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w is Modrix.Views.Windows.ProjectWorkspace);
            _projectPath = (workspace as Modrix.Views.Windows.ProjectWorkspace)?.ViewModel?.CurrentProject?.Location;

            // Create the collection view for filtering
            _modElementsView = CollectionViewSource.GetDefaultView(_modElements);
            _modElementsView.Filter = FilterModElements;

            // Load project elements
            LoadModElements();
        }

        private bool FilterModElements(object item)
        {
            if (!(item is ModElementData element))
                return false;
                
            // Apply search filter
            if (!string.IsNullOrEmpty(SearchText) && 
                !element.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !element.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            
            // Apply type filter
            if (!string.IsNullOrEmpty(SelectedFilterOption) && SelectedFilterOption != "All Elements")
            {
                switch (SelectedFilterOption)
                {
                    case "Items Only":
                        return element is ItemModElementData;
                    case "Blocks Only":
                        return element.Type == "block";
                    case "Entities Only":
                        return element.Type == "entity";
                    default:
                        return true;
                }
            }
            
            return true;
        }

        public async void LoadModElements()
        {
            try
            {
                if (string.IsNullOrEmpty(_projectPath))
                {
                    return;
                }

                _elementManager = new ModElementManager(_projectPath);
                await _elementManager.LoadElementsAsync();
                
                _modElements.Clear();
                foreach (var element in _elementManager.Elements)
                {
                    _modElements.Add(element);
                }
                
                // Update binding
                OnPropertyChanged(nameof(ModElements));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading mod elements: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenAddElementFlyout()
        {
            IsAddElementFlyoutOpen = true;
            OnPropertyChanged(nameof(IsAddElementFlyoutOpen));
        }

        private void AddModElement(IModElementGenerator generator)
        {
            IsAddElementFlyoutOpen = false;
            OnPropertyChanged(nameof(IsAddElementFlyoutOpen));
            
            // Create a new tab for the generator's page
            var page = generator.CreatePage();
            
            // Create a Frame to host the Page (Page can only have Window or Frame as parent)
            var frame = new Frame();
            frame.NavigationUIVisibility = NavigationUIVisibility.Hidden;
            frame.Content = page;
            
            // Create header with close button
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new Image { Source = new BitmapImage(new Uri(generator.Icon, UriKind.RelativeOrAbsolute)), Width = 20, Height = 20, Margin = new Thickness(0,0,4,0) },
                    new TextBlock { Text = generator.Name, VerticalAlignment = VerticalAlignment.Center }
                }
            };
            
            var closeButton = new Button { Content = "×", Width = 20, Height = 20, Padding = new Thickness(0), Margin = new Thickness(4,0,0,0) };
            header.Children.Add(closeButton);
            
            // Create the tab item with the frame as content
            var tabItem = new TabItem
            {
                Header = header,
                Content = frame
            };
            
            // Add close button functionality
            closeButton.Click += (s, e) => 
            {
                WorkspaceTabs.Items.Remove(tabItem);
                LoadModElements(); // Refresh after closing in case changes were made
            };
            
            // Add the tab and select it
            WorkspaceTabs.Items.Add(tabItem);
            WorkspaceTabs.SelectedItem = tabItem;
        }

        private async void EditModElement(ModElementData element)
        {
            // Create the appropriate editor based on element type
            if (element is ItemModElementData itemData)
            {
                var itemPage = new ItemGeneratorPage(itemData);
                
                // Create a Frame to host the Page
                var frame = new Frame();
                frame.NavigationUIVisibility = NavigationUIVisibility.Hidden;
                frame.Content = itemPage;
                
                // Create header with close button
                var header = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new Image { Source = new BitmapImage(new Uri(itemData.Icon, UriKind.RelativeOrAbsolute)), Width = 20, Height = 20, Margin = new Thickness(0,0,4,0) },
                        new TextBlock { Text = $"Edit: {itemData.Name}", VerticalAlignment = VerticalAlignment.Center }
                    }
                };
                
                var closeButton = new Button { Content = "×", Width = 20, Height = 20, Padding = new Thickness(0), Margin = new Thickness(4,0,0,0) };
                header.Children.Add(closeButton);
                
                // Create the tab item
                var tabItem = new TabItem
                {
                    Header = header,
                    Content = frame
                };
                
                // Add close button functionality
                closeButton.Click += (s, e) => 
                {
                    WorkspaceTabs.Items.Remove(tabItem);
                    LoadModElements(); // Refresh after closing to show any updates
                };
                
                // Add the tab and select it
                WorkspaceTabs.Items.Add(tabItem);
                WorkspaceTabs.SelectedItem = tabItem;
            }
            else
            {
                MessageBox.Show("Editor for this element type is not yet implemented.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void DeleteModElement(ModElementData element)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to delete the {element.Type} '{element.Name}'? This cannot be undone.",
                "Confirm Deletion",
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);
                
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _elementManager.DeleteElementAsync(element);
                    _modElements.Remove(element);
                    OnPropertyChanged(nameof(ModElements));
                    MessageBox.Show($"{element.Type} '{element.Name}' has been deleted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting element: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Simple RelayCommand implementation for demo
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;
        public RelayCommand(Action execute, Func<bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool>? _canExecute;
        public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T)parameter!) ?? true;
        public void Execute(object? parameter) => _execute((T)parameter!);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}



