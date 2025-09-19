using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using HelixToolkit.Wpf;

namespace Modrix.ViewModels.Pages
{
    public struct Size3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        
        public Size3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class ModelViewerViewModel : INotifyPropertyChanged
    {
        private string _modelPath;
        private string _modelName;
        private Model3DGroup _model3D;
        private bool _isLoading;
        private string _statusMessage;
        private Dictionary<string, string> _textureReferences = new();
        private string _modelJson;
        private string _projectPath;
        private string _modId;

        public string ModelPath
        {
            get => _modelPath;
            set
            {
                _modelPath = value;
                OnPropertyChanged();
                LoadModel();
            }
        }

        public string ModelName
        {
            get => _modelName;
            set
            {
                _modelName = value;
                OnPropertyChanged();
            }
        }

        public Model3DGroup Model3D
        {
            get => _model3D;
            set
            {
                _model3D = value;
                OnPropertyChanged();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public Dictionary<string, string> TextureReferences
        {
            get => _textureReferences;
            set
            {
                _textureReferences = value;
                OnPropertyChanged();
            }
        }

        public string ModelJson
        {
            get => _modelJson;
            set
            {
                _modelJson = value;
                OnPropertyChanged();
            }
        }

        public void SetProjectInfo(string projectPath, string modId)
        {
            _projectPath = projectPath;
            _modId = modId;
        }

        private async void LoadModel()
        {
            if (string.IsNullOrEmpty(_modelPath) || !File.Exists(_modelPath))
                return;

            IsLoading = true;
            StatusMessage = "Loading model...";

            try
            {
                // Read and parse the JSON model file
                var jsonContent = await File.ReadAllTextAsync(_modelPath);
                ModelJson = FormatJson(jsonContent);
                
                var modelData = JsonSerializer.Deserialize<JsonElement>(jsonContent);
                
                // Extract texture references
                ExtractTextureReferences(modelData);
                
                // Create 3D representation on UI thread
                await CreateMinecraft3DModel(modelData);
                
                StatusMessage = "Model loaded successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading model: {ex.Message}";
                Model3D = null;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string FormatJson(string json)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return json; // Return original if formatting fails
            }
        }

        private void ExtractTextureReferences(JsonElement modelData)
        {
            _textureReferences.Clear();
            
            if (modelData.TryGetProperty("textures", out var textures) && 
                textures.ValueKind == JsonValueKind.Object)
            {
                foreach (var texture in textures.EnumerateObject())
                {
                    _textureReferences[texture.Name] = texture.Value.GetString() ?? "";
                }
            }
            
            OnPropertyChanged(nameof(TextureReferences));
        }

        private async System.Threading.Tasks.Task CreateMinecraft3DModel(JsonElement modelData)
        {
            var group = new Model3DGroup();
            
            // Check if this model has a parent and try to resolve it first
            if (modelData.TryGetProperty("parent", out var parentProperty))
            {
                var parentPath = parentProperty.GetString();
                if (!string.IsNullOrEmpty(parentPath))
                {
                    await ResolveParentModel(parentPath, group);
                }
            }
            
            // Check if this is a block model with elements
            if (modelData.TryGetProperty("elements", out var elements) && 
                elements.ValueKind == JsonValueKind.Array && elements.GetArrayLength() > 0)
            {
                StatusMessage = "Rendering block model elements...";
                // Remove Task.Run - execute on UI thread
                CreateBlockModel(elements, group, modelData);
            }
            else
            {
                // This is likely an item model or a model that inherits from a parent
                StatusMessage = "Rendering item/inherited model...";
                // Remove Task.Run - execute on UI thread
                CreateItemModel(group, modelData);
            }
            
            // If no geometry was created, add a placeholder
            if (group.Children.Count == 0)
            {
                CreatePlaceholderModel(group);
            }
            
            Model3D = group;
        }

        private async System.Threading.Tasks.Task ResolveParentModel(string parentPath, Model3DGroup group)
        {
            try
            {
                // Handle Minecraft built-in parents (like "item/generated")
                if (parentPath.StartsWith("minecraft:"))
                {
                    StatusMessage = $"Using Minecraft parent: {parentPath}";
                    // For built-in parents like "item/generated", we'll create a simple item model
                    if (parentPath == "minecraft:item/generated")
                    {
                        CreateItemModel(group, new JsonElement()); // Empty element for built-in parent
                    }
                    return;
                }
                
                // Handle mod-specific parents
                if (!string.IsNullOrEmpty(_projectPath) && !string.IsNullOrEmpty(_modId))
                {
                    var cleanPath = parentPath;
                    if (cleanPath.StartsWith(_modId + ":"))
                    {
                        cleanPath = cleanPath.Substring(_modId.Length + 1);
                    }
                    
                    var parentModelPath = Path.Combine(_projectPath, "src", "main", "resources", "assets", _modId, "models", cleanPath + ".json");
                    
                    if (File.Exists(parentModelPath))
                    {
                        StatusMessage = $"Loading parent model: {parentPath}";
                        var parentJson = await File.ReadAllTextAsync(parentModelPath);
                        var parentData = JsonSerializer.Deserialize<JsonElement>(parentJson);
                        
                        // Extract parent textures and merge with current textures
                        if (parentData.TryGetProperty("textures", out var parentTextures) && 
                            parentTextures.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var texture in parentTextures.EnumerateObject())
                            {
                                if (!_textureReferences.ContainsKey(texture.Name))
                                {
                                    _textureReferences[texture.Name] = texture.Value.GetString() ?? "";
                                }
                            }
                        }
                        
                        // If parent has elements, load them
                        if (parentData.TryGetProperty("elements", out var parentElements) && 
                            parentElements.ValueKind == JsonValueKind.Array && parentElements.GetArrayLength() > 0)
                        {
                            CreateBlockModel(parentElements, group, parentData);
                        }
                        else
                        {
                            // Parent is likely an item model
                            CreateItemModel(group, parentData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not resolve parent model {parentPath}: {ex.Message}";
            }
        }

        private void CreatePlaceholderModel(Model3DGroup group)
        {
            StatusMessage = "Creating placeholder model (no elements or parent found)";
            
            var meshBuilder = new MeshBuilder();
            meshBuilder.AddBox(new Point3D(0, 0, 0), 0.5, 0.5, 0.5);
            
            var material = new DiffuseMaterial(new SolidColorBrush(Colors.Gray));
            var geometry = new GeometryModel3D(meshBuilder.ToMesh(), material);
            
            group.Children.Add(geometry);
        }

        private void CreateBlockModel(JsonElement elements, Model3DGroup group, JsonElement modelData)
        {
            foreach (var element in elements.EnumerateArray())
            {
                CreateElementMesh(element, group, modelData);
            }
        }

        private void CreateItemModel(Model3DGroup group, JsonElement modelData)
        {
            // For item models, create a flat plane to display the texture
            var meshBuilder = new MeshBuilder();
            
            // Create a flat rectangle (like a card)
            var width = 1.0;
            var height = 1.0;
            
            // Define the four corners of the rectangle
            var p1 = new Point3D(-width/2, -height/2, 0);
            var p2 = new Point3D(width/2, -height/2, 0);
            var p3 = new Point3D(width/2, height/2, 0);
            var p4 = new Point3D(-width/2, height/2, 0);
            
            // Add the rectangle (two triangles)
            meshBuilder.AddQuad(p1, p2, p3, p4);
            
            // Try to load the actual texture
            var material = CreateItemMaterial(modelData);
            
            var geometry = new GeometryModel3D(meshBuilder.ToMesh(), material);
            
            // Make it double-sided for item models
            geometry.BackMaterial = material;
            
            group.Children.Add(geometry);
        }

        private void CreateElementMesh(JsonElement element, Model3DGroup group, JsonElement modelData)
        {
            try
            {
                // Parse element dimensions (Minecraft uses 16x16x16 unit system)
                var from = ParseVector3(element.GetProperty("from"));
                var to = ParseVector3(element.GetProperty("to"));
                
                // Convert Minecraft coordinates to 3D coordinates (scale and center)
                var scale = 0.0625; // 1/16 for proper Minecraft scaling
                var fromScaled = new Point3D(
                    (from.X - 8) * scale,
                    (from.Y - 8) * scale,
                    (from.Z - 8) * scale
                );
                var toScaled = new Point3D(
                    (to.X - 8) * scale,
                    (to.Y - 8) * scale,
                    (to.Z - 8) * scale
                );
                
                // Create a box mesh with proper UV mapping
                var meshBuilder = new MeshBuilder();
                var center = new Point3D(
                    (fromScaled.X + toScaled.X) / 2,
                    (fromScaled.Y + toScaled.Y) / 2,
                    (fromScaled.Z + toScaled.Z) / 2
                );
                var size = new Size3D(
                    Math.Abs(toScaled.X - fromScaled.X),
                    Math.Abs(toScaled.Y - fromScaled.Y),
                    Math.Abs(toScaled.Z - fromScaled.Z)
                );
                
                meshBuilder.AddBox(center, size.X, size.Y, size.Z);
                
                // Create material with texture if available
                var material = CreateElementMaterial(element, modelData);
                
                var geometry = new GeometryModel3D(meshBuilder.ToMesh(), material);
                group.Children.Add(geometry);
            }
            catch (Exception ex)
            {
                // Skip invalid elements but log the error
                StatusMessage = $"Warning: Skipped invalid element - {ex.Message}";
            }
        }

        private Material CreateItemMaterial(JsonElement modelData)
        {
            // For item models, try to get the main texture
            if (_textureReferences.ContainsKey("layer0"))
            {
                var texture = ResolveTexture("#layer0", modelData);
                if (texture != null)
                {
                    return new DiffuseMaterial(new ImageBrush(texture) { Stretch = Stretch.Uniform });
                }
            }
            
            // Try other common texture references
            foreach (var texRef in _textureReferences)
            {
                var texture = ResolveTexture($"#{texRef.Key}", modelData);
                if (texture != null)
                {
                    return new DiffuseMaterial(new ImageBrush(texture) { Stretch = Stretch.Uniform });
                }
            }
            
            // Create a checkerboard pattern as fallback
            return new DiffuseMaterial(new SolidColorBrush(Colors.Pink)); // Pink to indicate missing texture
        }

        private Material CreateElementMaterial(JsonElement element, JsonElement modelData)
        {
            // Try to get the faces and their textures
            if (element.TryGetProperty("faces", out var faces) && faces.ValueKind == JsonValueKind.Object)
            {
                // For now, just get any face texture
                foreach (var face in faces.EnumerateObject())
                {
                    if (face.Value.TryGetProperty("texture", out var textureRef))
                    {
                        var textureKey = textureRef.GetString();
                        if (!string.IsNullOrEmpty(textureKey))
                        {
                            var texture = ResolveTexture(textureKey, modelData);
                            if (texture != null)
                            {
                                return new DiffuseMaterial(new ImageBrush(texture));
                            }
                        }
                    }
                }
            }
            
            // Fallback to a colored material (different colors for different elements)
            return new DiffuseMaterial(new SolidColorBrush(GetRandomColor()));
        }

        private BitmapImage ResolveTexture(string textureReference, JsonElement modelData)
        {
            try
            {
                string texturePath = textureReference;
                
                // Resolve texture variables (e.g., #layer0 -> actual texture path)
                if (texturePath.StartsWith("#"))
                {
                    var variableName = texturePath.Substring(1);
                    if (_textureReferences.ContainsKey(variableName))
                    {
                        texturePath = _textureReferences[variableName];
                    }
                    else
                    {
                        StatusMessage = $"Texture variable '{variableName}' not found in texture references";
                        return null;
                    }
                }
                
                // Clean up the texture path
                if (!string.IsNullOrEmpty(_modId) && texturePath.StartsWith(_modId + ":"))
                {
                    texturePath = texturePath.Substring(_modId.Length + 1);
                }
                
                // Build the full file path
                if (!string.IsNullOrEmpty(_projectPath) && !string.IsNullOrEmpty(_modId))
                {
                    var fullTexturePath = Path.Combine(_projectPath, "src", "main", "resources", "assets", _modId, "textures", texturePath + ".png");
                    
                    StatusMessage = $"Looking for texture: {fullTexturePath}";
                    
                    if (File.Exists(fullTexturePath))
                    {
                        // Create BitmapImage on UI thread
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(fullTexturePath, UriKind.Absolute);
                        bitmap.EndInit();
                        bitmap.Freeze(); // Make it thread-safe
                        StatusMessage = $"Successfully loaded texture: {Path.GetFileName(fullTexturePath)}";
                        return bitmap;
                    }
                    else
                    {
                        StatusMessage = $"Texture file not found: {fullTexturePath}";
                    }
                }
                else
                {
                    StatusMessage = "Project path or mod ID not set - cannot load textures";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not load texture: {textureReference} - {ex.Message}";
            }
            
            return null;
        }

        private Point3D ParseVector3(JsonElement vectorElement)
        {
            if (vectorElement.ValueKind == JsonValueKind.Array)
            {
                var values = vectorElement.EnumerateArray().ToArray();
                return new Point3D(
                    values.Length > 0 ? values[0].GetDouble() : 0,
                    values.Length > 1 ? values[1].GetDouble() : 0,
                    values.Length > 2 ? values[2].GetDouble() : 0
                );
            }
            return new Point3D(0, 0, 0);
        }

        private Color GetRandomColor()
        {
            var colors = new[] { Colors.Brown, Colors.Gray, Colors.Green, Colors.Blue, Colors.Red, Colors.Yellow, Colors.Purple, Colors.Orange };
            var random = new Random();
            return colors[random.Next(colors.Length)];
        }

        private ImageSource CreateCheckerboardTexture()
        {
            try
            {
                var width = 16;
                var height = 16;
                var bitmap = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgr24, null);
                
                var stride = (width * bitmap.Format.BitsPerPixel + 7) / 8;
                var pixels = new byte[height * stride];
                
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var index = y * stride + x * 3;
                        var isEven = (x / 4 + y / 4) % 2 == 0;
                        var color = isEven ? (byte)255 : (byte)200; // White or light gray
                        
                        pixels[index] = color;     // Blue
                        pixels[index + 1] = color; // Green  
                        pixels[index + 2] = color; // Red
                    }
                }
                
                bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, stride, 0);
                bitmap.Freeze();
                
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}