using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Modrix.ViewModels.Windows;
using Modrix.ModElements;
using System;
using System.IO;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using Modrix.Models;
using System.Linq;

namespace Modrix.Views.Pages
{
    public partial class ItemGeneratorPage : Page
    {
        private ModElementManager? _elementManager;
        private string? _projectPath;
        private bool _isEditing;
        private ItemModElementData? _existingItem;
        private ModProjectData? _currentProject;

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
                var workspaceTyped = workspace as Modrix.Views.Windows.ProjectWorkspace;
                _currentProject = workspaceTyped?.ViewModel?.CurrentProject;
                _projectPath = _currentProject?.Location;
                
                if (!string.IsNullOrEmpty(_projectPath) && Directory.Exists(_projectPath))
                {
                    _elementManager = new ModElementManager(_projectPath);
                }
                else
                {
                    ShowMessage("Warning", "Could not determine project path. Please ensure a project is loaded.");
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Error", $"Error initializing item generator: {ex.Message}");
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
                if (NameTextBox != null)
                {
                    NameTextBox.Text = ItemName;
                }
                if (TexturePicker != null)
                {
                    TexturePicker.SelectedTexturePath = TexturePath;
                }
                
                if (IsFood && FoodPropertiesPanel != null)
                {
                    FoodPropertiesPanel.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Error", $"Error populating item data: {ex.Message}");
            }
        }

        private async void ShowMessage(string title, string message)
        {
            var msgBox = new MessageBox
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "OK"
            };
            await msgBox.ShowDialogAsync();
        }

        // Properties for data binding
        public ModProjectData? CurrentProject => _currentProject;
        public string MinecraftVersion => _currentProject?.MinecraftVersion ?? "1.20.1";
        public string ItemName { get; set; } = "";
        public string TexturePath { get; set; } = "";
        public int MaxStackSize { get; set; } = 64;
        public bool HasGlint { get; set; }
        public bool IsFood { get; set; }
        public int FoodValue { get; set; }
        public float SaturationValue { get; set; }

        private void TexturePicker_TextureSelected(object sender, string? texturePath)
        {
            TexturePath = texturePath ?? "";
        }

        private async Task ShowMessageAsync(string title, string message)
        {
            var msgBox = new MessageBox
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "OK"
            };
            await msgBox.ShowDialogAsync();
        }

        private async void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Close or navigate away
            try
            {
                var nav = NavigationService;
                if (nav != null && nav.CanGoBack) nav.GoBack();
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Error", $"Error navigating back: {ex.Message}");
            }
        }

        private async void Create_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(ItemName))
            {
                await ShowMessageAsync("Validation Error", "Item name is required.");
                NameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(TexturePath) || !File.Exists(TexturePath))
            {
                await ShowMessageAsync("Validation Error", "Please select a valid texture file.");
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
                    await ShowMessageAsync("Error", "Could not create element manager. Project path may be invalid.");
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
                    string translationKey = $"item.{_currentProject?.ModId?.ToLower() ?? "mod"}.{ItemName.ToLower().Replace(" ", "_")}";
                    
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
                await ShowMessageAsync("Success", $"Item '{ItemName}' has been {(_isEditing ? "updated" : "created")} successfully!");

                // Navigate away
                var nav = NavigationService;
                if (nav != null && nav.CanGoBack) nav.GoBack();
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Error", $"Error creating item: {ex.Message}");
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
