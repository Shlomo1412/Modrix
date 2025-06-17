using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Modrix.ViewModels.Windows;
using Modrix.ModElements;
using System;
using System.IO;

namespace Modrix.Views.Pages
{
    public partial class ItemGeneratorPage : Page
    {
        private ModElementManager? _elementManager;
        private string? _projectPath;
        private bool _isEditing;
        private ItemModElementData? _existingItem;

        public ItemGeneratorPage()
        {
            InitializeComponent();
            DataContext = this;

            // Get the current project path from the workspace view model
            try
            {
                var workspace = Application.Current.Windows
                    .OfType<Window>()
                    .FirstOrDefault(w => w is Modrix.Views.Windows.ProjectWorkspace);
                _projectPath = (workspace as Modrix.Views.Windows.ProjectWorkspace)?.ViewModel?.CurrentProject?.Location;
                
                if (!string.IsNullOrEmpty(_projectPath) && Directory.Exists(_projectPath))
                {
                    _elementManager = new ModElementManager(_projectPath);
                }
                else
                {
                    MessageBox.Show("Could not determine project path. Please ensure a project is loaded.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing item generator: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            // Initialize default values
            MaxStackSize = 64;
            ItemName = "";
            TexturePath = "";
        }

        public ItemGeneratorPage(ItemModElementData item) : this()
        {
            if (item == null) return;
            
            // Constructor for editing existing item
            _isEditing = true;
            _existingItem = item;
            
            // Populate fields
            ItemName = item.Name ?? "";
            TexturePath = item.TexturePath ?? "";
            MaxStackSize = item.MaxStackSize;
            HasGlint = item.HasGlint;
            IsFood = item.IsFood;
            FoodValue = item.FoodValue;
            SaturationValue = item.SaturationValue;
            
            // Update UI to reflect editing mode
            try
            {
                NameTextBox.Text = ItemName;
                TextureTextBox.Text = TexturePath;
                
                if (IsFood && FoodPropertiesPanel != null)
                {
                    FoodPropertiesPanel.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error populating item data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public string ItemName { get; set; } = "";
        public string TexturePath { get; set; } = "";
        public int MaxStackSize { get; set; } = 64;
        public bool HasGlint { get; set; }
        public bool IsFood { get; set; }
        public int FoodValue { get; set; }
        public float SaturationValue { get; set; }

        private void ChooseTexture_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the current project path from the workspace view model
                var workspace = Application.Current.Windows
                    .OfType<Window>()
                    .FirstOrDefault(w => w is Modrix.Views.Windows.ProjectWorkspace);
                    
                var projectPath = (workspace as Modrix.Views.Windows.ProjectWorkspace)?.ViewModel?.CurrentProject?.Location;
                
                if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
                {
                    MessageBox.Show("Could not determine project path. Please ensure a project is loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var dialog = new Windows.ChooseTextureDialog(projectPath);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedTexturePath))
                {
                    // Set the texture path
                    TexturePath = dialog.SelectedTexturePath;
                    TextureTextBox.Text = TexturePath;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error choosing texture: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Close or navigate away
            try
            {
                var nav = NavigationService;
                if (nav != null && nav.CanGoBack) nav.GoBack();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error navigating back: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Create_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(ItemName))
            {
                MessageBox.Show("Item name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(TexturePath) || !File.Exists(TexturePath))
            {
                MessageBox.Show("Please select a valid texture file.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Initialize element manager if needed
                if (_elementManager == null && !string.IsNullOrEmpty(_projectPath) && Directory.Exists(_projectPath))
                {
                    _elementManager = new ModElementManager(_projectPath);
                }

                if (_elementManager == null)
                {
                    MessageBox.Show("Could not create element manager. Project path may be invalid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Create or update item data
                ItemModElementData itemData;
                if (_isEditing && _existingItem != null)
                {
                    // Update existing item
                    itemData = _existingItem;
                    itemData.Name = ItemName;
                    itemData.TexturePath = TexturePath;
                    itemData.MaxStackSize = MaxStackSize;
                    itemData.HasGlint = HasGlint;
                    itemData.IsFood = IsFood;
                    itemData.FoodValue = FoodValue;
                    itemData.SaturationValue = SaturationValue;
                    itemData.UpdateLastModified();
                }
                else
                {
                    // Generate a valid translation key
                    string translationKey = $"item.{_projectPath?.Split('\\').Last()?.ToLower() ?? "mod"}.{ItemName.ToLower().Replace(" ", "_")}";
                    
                    // Create new item
                    itemData = new ItemModElementData
                    {
                        Name = ItemName,
                        TexturePath = TexturePath,
                        MaxStackSize = MaxStackSize,
                        HasGlint = HasGlint,
                        IsFood = IsFood,
                        FoodValue = FoodValue,
                        SaturationValue = SaturationValue,
                        Description = $"Custom item: {ItemName}",
                        TranslationKey = translationKey,
                        IconPath = TexturePath
                    };
                }

                // Save the item data
                await _elementManager.SaveElementAsync(itemData);

                // Generate the code
                var generator = new ModElements.Generators.ItemModElementGenerator();
                await _elementManager.GenerateCodeAsync(itemData, generator);

                // Show success message
                MessageBox.Show($"Item '{ItemName}' has been {(_isEditing ? "updated" : "created")} successfully!", 
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Navigate away
                var nav = NavigationService;
                if (nav != null && nav.CanGoBack) nav.GoBack();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void IsFood_Checked(object sender, RoutedEventArgs e)
        {
            if (FoodPropertiesPanel != null)
            {
                FoodPropertiesPanel.Visibility = IsFood ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void IsFood_Unchecked(object sender, RoutedEventArgs e)
        {
            if (FoodPropertiesPanel != null)
            {
                FoodPropertiesPanel.Visibility = Visibility.Collapsed;
            }
        }
    }
}
