using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Abstractions.Controls;

namespace Modrix.ViewModels.Pages;

public enum EditorTool { Pencil, Eraser, Bucket, Picker }

public partial class TextureEditorViewModel : ObservableObject, INavigationAware
{
    [ObservableProperty]
    private string _pngPath = string.Empty;

    [ObservableProperty]
    private BitmapSource? _currentImage;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private int _imageWidth;

    [ObservableProperty]
    private int _imageHeight;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private EditorTool _currentTool = EditorTool.Pencil;

    [ObservableProperty]
    private Color _selectedColor = Colors.Black;

    [ObservableProperty]
    private SolidColorBrush _selectedColorBrush = new(Colors.Black);

    [ObservableProperty]
    private int _zoomLevel = 8; // Default 800% zoom

    [ObservableProperty]
    private bool _isDrawing;

    [ObservableProperty]
    private int _redValue = 0;

    [ObservableProperty]
    private int _greenValue = 0;

    [ObservableProperty]
    private int _blueValue = 0;

    private WriteableBitmap? _bitmap;
    private Color[,]? _pixelData;

    public TextureEditorViewModel()
    {
        SaveCommand = new AsyncRelayCommand(SaveChangesAsync);
        SelectPencilCommand = new RelayCommand(() => CurrentTool = EditorTool.Pencil);
        SelectEraserCommand = new RelayCommand(() => CurrentTool = EditorTool.Eraser);
        SelectBucketCommand = new RelayCommand(() => CurrentTool = EditorTool.Bucket);
        SelectPickerCommand = new RelayCommand(() => CurrentTool = EditorTool.Picker);
        ZoomInCommand = new RelayCommand(ZoomIn);
        ZoomOutCommand = new RelayCommand(ZoomOut);
    }

    public IAsyncRelayCommand SaveCommand { get; }
    public ICommand SelectPencilCommand { get; }
    public ICommand SelectEraserCommand { get; }
    public ICommand SelectBucketCommand { get; }
    public ICommand SelectPickerCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }

    public void SetPngPath(string path)
    {
        PngPath = path;
        if (!string.IsNullOrEmpty(PngPath))
        {
            LoadImage();
        }
    }

    public Task OnNavigatedToAsync() => Task.CompletedTask;
    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private void LoadImage()
    {
        if (!File.Exists(PngPath))
            return;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(PngPath);
        image.EndInit();

        ImageWidth = image.PixelWidth;
        ImageHeight = image.PixelHeight;
        FileName = Path.GetFileName(PngPath);
        HasUnsavedChanges = false;

        // Create writable bitmap for editing
        _bitmap = new WriteableBitmap(image);
        CurrentImage = _bitmap;
        UpdatePixelData();
    }

    private void UpdatePixelData()
    {
        if (_bitmap == null) return;

        _pixelData = new Color[_bitmap.PixelWidth, _bitmap.PixelHeight];
        var stride = _bitmap.PixelWidth * (_bitmap.Format.BitsPerPixel / 8);
        var pixelData = new byte[stride * _bitmap.PixelHeight];
        _bitmap.CopyPixels(pixelData, stride, 0);

        // Convert the bitmap to a format we can work with
        var convertedBitmap = new FormatConvertedBitmap(_bitmap, PixelFormats.Bgra32, null, 0);
        var convertedPixelData = new byte[_bitmap.PixelWidth * _bitmap.PixelHeight * 4];
        convertedBitmap.CopyPixels(convertedPixelData, _bitmap.PixelWidth * 4, 0);

        for (int y = 0; y < _bitmap.PixelHeight; y++)
        {
            for (int x = 0; x < _bitmap.PixelWidth; x++)
            {
                int index = (y * _bitmap.PixelWidth + x) * 4;
                _pixelData[x, y] = Color.FromArgb(
                    convertedPixelData[index + 3],
                    convertedPixelData[index + 2],
                    convertedPixelData[index + 1],
                    convertedPixelData[index]);
            }
        }
    }

    public void HandlePixelAction(int x, int y)
    {
        if (_bitmap == null || _pixelData == null ||
            x < 0 || x >= ImageWidth || y < 0 || y >= ImageHeight)
            return;

        try
        {
            switch (CurrentTool)
            {
                case EditorTool.Pencil:
                    SetPixel(x, y, SelectedColor);
                    break;

                case EditorTool.Eraser:
                    SetPixel(x, y, Colors.Transparent);
                    break;

                case EditorTool.Picker:
                    var color = _pixelData[x, y];
                    SelectedColor = color;
                    SelectedColorBrush = new SolidColorBrush(color);
                    UpdateRgbValues(color);
                    break;

                case EditorTool.Bucket:
                    var targetColor = _pixelData[x, y];
                    if (targetColor != SelectedColor)
                    {
                        FloodFill(x, y, targetColor, SelectedColor);
                    }
                    break;
            }

            HasUnsavedChanges = true;
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    private void SetPixel(int x, int y, Color color)
    {
        if (_bitmap == null || _pixelData == null) return;

        try
        {
            // Safe pixel writing without unsafe code
            var rect = new Int32Rect(x, y, 1, 1);
            byte[] colorData = { color.B, color.G, color.R, color.A };
            _bitmap.WritePixels(rect, colorData, 4, 0);

            _pixelData[x, y] = color;
        }
        catch (Exception ex)
        {
            StatusText = $"Pixel error: {ex.Message}";
        }
    }

    private void FloodFill(int x, int y, Color targetColor, Color replacementColor)
    {
        if (targetColor == replacementColor) return;
        if (_pixelData == null) return;

        var queue = new Queue<Point>();
        queue.Enqueue(new Point(x, y));
        var visited = new bool[ImageWidth, ImageHeight];

        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            int px = (int)point.X;
            int py = (int)point.Y;

            if (px < 0 || px >= ImageWidth || py < 0 || py >= ImageHeight || visited[px, py])
                continue;

            if (_pixelData[px, py] != targetColor)
                continue;

            SetPixel(px, py, replacementColor);
            visited[px, py] = true;

            queue.Enqueue(new Point(px - 1, py));
            queue.Enqueue(new Point(px + 1, py));
            queue.Enqueue(new Point(px, py - 1));
            queue.Enqueue(new Point(px, py + 1));
        }
    }

    public void UpdateCursorPosition(int x, int y)
    {
        StatusText = $"X: {x / ZoomLevel}, Y: {y / ZoomLevel}";
    }

    private void ZoomIn()
    {
        if (ZoomLevel < 32) ZoomLevel += 1;
    }

    private void ZoomOut()
    {
        if (ZoomLevel > 1) ZoomLevel -= 1;
    }

    private async Task SaveChangesAsync()
    {
        if (!HasUnsavedChanges || _bitmap == null)
            return;

        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(_bitmap));

            using (var fileStream = File.Create(PngPath))
            {
                encoder.Save(fileStream);
            }

            HasUnsavedChanges = false;
            StatusText = "Saved successfully";
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
        }
    }

    partial void OnSelectedColorChanged(Color value)
    {
        SelectedColorBrush = new SolidColorBrush(value);
        UpdateRgbValues(value);
    }

    private void UpdateRgbValues(Color color)
    {
        RedValue = color.R;
        GreenValue = color.G;
        BlueValue = color.B;
    }

    partial void OnRedValueChanged(int value)
    {
        SelectedColor = Color.FromArgb(255, (byte)value, (byte)GreenValue, (byte)BlueValue);
    }

    partial void OnGreenValueChanged(int value)
    {
        SelectedColor = Color.FromArgb(255, (byte)RedValue, (byte)value, (byte)BlueValue);
    }

    partial void OnBlueValueChanged(int value)
    {
        SelectedColor = Color.FromArgb(255, (byte)RedValue, (byte)GreenValue, (byte)value);
    }

    partial void OnZoomLevelChanged(int value)
    {
        StatusText = $"Zoom: {value * 100}%";
    }
}