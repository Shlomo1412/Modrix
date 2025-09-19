using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Modrix.Services;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace Modrix.Views.Windows
{
    public partial class MissingMappingsDialog : FluentWindow
    {
        private readonly string _projectPath;
        private readonly string _modId;
        private List<string> _availableTextures;
        private readonly ObservableCollection<MappingIssueViewModel> _mappingIssues;

        public bool HasChanges { get; private set; }

        public MissingMappingsDialog(string projectPath, string modId, List<ModelValidationService.MissingMapping> missingMappings)
        {
            InitializeComponent();
            
            _projectPath = projectPath;
            _modId = modId;
            _mappingIssues = new ObservableCollection<MappingIssueViewModel>();
            
            LoadAvailableTextures();
            LoadMappingIssues(missingMappings);
            
            MappingsList.ItemsSource = _mappingIssues;
        }

        private void LoadAvailableTextures()
        {
            _availableTextures = new List<string>();
            
            var texturesPath = Path.Combine(_projectPath, "src", "main", "resources", "assets", _modId, "textures");
            if (!Directory.Exists(texturesPath)) return;

            var textureFiles = Directory.GetFiles(texturesPath, "*.png", SearchOption.AllDirectories);
            
            foreach (var file in textureFiles)
            {
                var relativePath = Path.GetRelativePath(texturesPath, file)
                    .Replace('\\', '/')
                    .Replace(".png", "");
                _availableTextures.Add(relativePath);
            }
        }

        private void LoadMappingIssues(List<ModelValidationService.MissingMapping> missingMappings)
        {
            _mappingIssues.Clear();
            
            foreach (var mapping in missingMappings)
            {
                var viewModel = new MappingIssueViewModel
                {
                    ModelPath = mapping.ModelPath,
                    ModelFileName = Path.GetFileName(mapping.ModelPath),
                    ReferencedTexture = mapping.ReferencedTexture,
                    SuggestedTexture = mapping.SuggestedTexture,
                    TexturePath = mapping.TexturePath,
                    HasSuggestion = !string.IsNullOrEmpty(mapping.SuggestedTexture),
                    NewTexturePath = mapping.SuggestedTexture
                };
                
                _mappingIssues.Add(viewModel);
            }
        }

        private void AutoFixAll_Click(object sender, RoutedEventArgs e)
        {
            var fixedCount = 0;
            
            foreach (var issue in _mappingIssues.Where(i => i.HasSuggestion).ToList())
            {
                issue.NewTexturePath = issue.SuggestedTexture;
                issue.IsFixed = true;
                fixedCount++;
            }
            
            ShowMessage($"Auto-fixed {fixedCount} mappings. Review and save changes.", "Auto-Fix Complete");
        }

        private void RefreshTextures_Click(object sender, RoutedEventArgs e)
        {
            LoadAvailableTextures();
            ShowMessage($"Found {_availableTextures.Count} textures.", "Textures Refreshed");
        }

        private void BrowseTexture_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Wpf.Ui.Controls.Button button || button.Tag is not MappingIssueViewModel issue)
                return;

            var dialog = new OpenFileDialog
            {
                Title = "Select Texture File",
                Filter = "Image Files|*.png;*.jpg;*.jpeg",
                InitialDirectory = Path.Combine(_projectPath, "src", "main", "resources", "assets", _modId, "textures")
            };

            if (dialog.ShowDialog() == true)
            {
                var texturesPath = Path.Combine(_projectPath, "src", "main", "resources", "assets", _modId, "textures");
                var relativePath = Path.GetRelativePath(texturesPath, dialog.FileName)
                    .Replace('\\', '/')
                    .Replace(".png", "");
                
                issue.NewTexturePath = relativePath;
                issue.IsFixed = true;
            }
        }

        private void SkipMapping_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Wpf.Ui.Controls.Button button || button.Tag is not MappingIssueViewModel issue)
                return;

            _mappingIssues.Remove(issue);
        }

        private void ApplySuggestion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Wpf.Ui.Controls.Button button || button.Tag is not MappingIssueViewModel issue)
                return;

            issue.NewTexturePath = issue.SuggestedTexture;
            issue.IsFixed = true;
        }

        private void ApplyManual_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Wpf.Ui.Controls.Button button || button.Tag is not MappingIssueViewModel issue)
                return;

            // Find the corresponding TextBox
            var parent = button.Parent as Grid;
            var textBox = parent?.Children.OfType<Wpf.Ui.Controls.TextBox>().FirstOrDefault();
            
            if (textBox != null && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                issue.NewTexturePath = textBox.Text.Trim();
                issue.IsFixed = true;
                textBox.Text = string.Empty; // Clear the input
            }
        }

        private async void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var fixedIssues = _mappingIssues.Where(i => i.IsFixed).ToList();
                
                if (!fixedIssues.Any())
                {
                    ShowMessage("No changes to save.", "No Changes");
                    return;
                }

                foreach (var issue in fixedIssues)
                {
                    await UpdateModelFile(issue);
                }

                HasChanges = true;
                ShowMessage($"Successfully updated {fixedIssues.Count} model files.", "Changes Saved");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowMessage($"Error saving changes: {ex.Message}", "Error");
            }
        }

        private async System.Threading.Tasks.Task UpdateModelFile(MappingIssueViewModel issue)
        {
            var content = await File.ReadAllTextAsync(issue.ModelPath);
            var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            // Create a new JSON object with updated texture reference
            var options = new JsonWriterOptions { Indented = true };
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, options);

            writer.WriteStartObject();

            foreach (var property in root.EnumerateObject())
            {
                if (property.Name == "textures" && property.Value.ValueKind == JsonValueKind.Object)
                {
                    writer.WritePropertyName("textures");
                    writer.WriteStartObject();

                    foreach (var texture in property.Value.EnumerateObject())
                    {
                        writer.WritePropertyName(texture.Name);
                        
                        var textureValue = texture.Value.GetString();
                        if (textureValue == issue.ReferencedTexture)
                        {
                            // Update with new texture path
                            var newValue = issue.NewTexturePath.StartsWith(_modId + ":") 
                                ? issue.NewTexturePath 
                                : $"{_modId}:{issue.NewTexturePath}";
                            writer.WriteStringValue(newValue);
                        }
                        else
                        {
                            writer.WriteStringValue(textureValue);
                        }
                    }

                    writer.WriteEndObject();
                }
                else
                {
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
            writer.Flush();

            var updatedJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            await File.WriteAllTextAsync(issue.ModelPath, updatedJson);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void ShowMessage(string message, string title)
        {
            var messageBox = new MessageBox
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "OK"
            };
            await messageBox.ShowDialogAsync();
        }
    }

    public class MappingIssueViewModel
    {
        public string ModelPath { get; set; }
        public string ModelFileName { get; set; }
        public string ReferencedTexture { get; set; }
        public string SuggestedTexture { get; set; }
        public string TexturePath { get; set; }
        public string NewTexturePath { get; set; }
        public bool HasSuggestion { get; set; }
        public bool IsFixed { get; set; }
    }
}