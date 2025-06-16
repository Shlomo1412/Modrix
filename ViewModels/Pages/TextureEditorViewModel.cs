using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Abstractions.Controls;
using System.Windows.Controls; // <-- Add this for Frame

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
    private int _zoomLevel = 5; // Default 500% zoom

    [ObservableProperty]
    private bool _isDrawing;

    [ObservableProperty]
    private int _redValue = 0;

    [ObservableProperty]
    private int _greenValue = 0;

    [ObservableProperty]
    private int _blueValue = 0;

    [ObservableProperty]
    private Cursor _currentCursor = Cursors.Pen; // Default to Pen

    [ObservableProperty]
    private bool _showGrid = false; // New property for grid visibility

    [ObservableProperty]
    private int _hoverX = -1; // New property for hover position

    [ObservableProperty]
    private int _hoverY = -1; // New property for hover position

    private WriteableBitmap? _bitmap;
    private Color[,]? _pixelData;

    private Stack<Color[,]> _undoStack = new();
    private Stack<Color[,]> _redoStack = new();

    private static readonly Cursor PencilCursor = Cursors.Pen;
    private static readonly Cursor DefaultCursor = Cursors.Arrow;
    private static Cursor? EraserCursor;
    private static Cursor? PickerCursor;
    private static Cursor? BucketCursor;

    static TextureEditorViewModel()
    {
        // Load custom cursors from Resources/Cursors
        try { EraserCursor = new Cursor("Resources/Cursors/Eraser.cur"); } catch { EraserCursor = Cursors.Cross; }
        try { PickerCursor = new Cursor("Resources/Cursors/ColorPicker.cur"); } catch { PickerCursor = Cursors.IBeam; }
        try { BucketCursor = new Cursor("Resources/Cursors/Bucket.cur"); } catch { BucketCursor = Cursors.Hand; }
    }

    public TextureEditorViewModel()
    {
        SaveCommand = new AsyncRelayCommand(SaveChangesAsync);
        SelectPencilCommand = new RelayCommand(() => CurrentTool = EditorTool.Pencil);
        SelectEraserCommand = new RelayCommand(() => CurrentTool = EditorTool.Eraser);
        SelectBucketCommand = new RelayCommand(() => CurrentTool = EditorTool.Bucket);
        SelectPickerCommand = new RelayCommand(() => CurrentTool = EditorTool.Picker);
        ZoomInCommand = new RelayCommand(ZoomIn);
        ZoomOutCommand = new RelayCommand(ZoomOut);
        ToggleGridCommand = new RelayCommand(() => ShowGrid = !ShowGrid);
        UndoCommand = new RelayCommand(Undo, CanUndo);
        RedoCommand = new RelayCommand(Redo, CanRedo);
    }

    public IAsyncRelayCommand SaveCommand { get; }
    public ICommand SelectPencilCommand { get; }
    public ICommand SelectEraserCommand { get; }
    public ICommand SelectBucketCommand { get; }
    public ICommand SelectPickerCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ToggleGridCommand { get; }
    public IRelayCommand UndoCommand { get; }
    public IRelayCommand RedoCommand { get; }

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

        _undoStack.Clear();
        _redoStack.Clear();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
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

    private Color[,]? ClonePixelData(Color[,]? source)
    {
        if (source == null) return null;
        int width = source.GetLength(0);
        int height = source.GetLength(1);
        var clone = new Color[width, height];
        Array.Copy(source, clone, source.Length);
        return clone;
    }

    public void PushUndoState()
    {
        if (_pixelData != null)
        {
            _undoStack.Push(ClonePixelData(_pixelData)!);
            _redoStack.Clear(); // Clear redo stack on new action
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
            HasUnsavedChanges = true; // Any action that can be undone is an unsaved change
        }
    }

    public void HandlePixelAction(int x, int y)
    {
        if (_bitmap == null || _pixelData == null ||
            x < 0 || x >= ImageWidth || y < 0 || y >= ImageHeight)
            return;

        try
        {
            // For Pencil tool, do NOT push undo state here (handled in code-behind for drag)
            if (CurrentTool != EditorTool.Picker && CurrentTool != EditorTool.Pencil)
            {
                PushUndoState();
            }

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
                    if (!targetColor.Equals(SelectedColor))
                    {
                        FloodFill(x, y, targetColor, SelectedColor);
                    }
                    else // If target color is same as selected, no action, so pop the pushed undo state
                    {
                        if (_undoStack.Count > 0) _undoStack.Pop();
                        UndoCommand.NotifyCanExecuteChanged();
                    }
                    break;
            }
            // HasUnsavedChanges is now set in PushUndoState
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    private void SetPixel(int x, int y, Color color)
    {
        if (_bitmap == null || _pixelData == null) return;
        if (_pixelData[x, y] == color) return; // No change if color is the same

        try
        {
            // Create a color array with BGRA format (which is what WriteableBitmap expects)
            var colorData = new byte[] { color.B, color.G, color.R, color.A };

            // Lock the bitmap for writing
            _bitmap.Lock();

            try
            {
                // Write the pixel
                Int32Rect rect = new Int32Rect(x, y, 1, 1);
                _bitmap.WritePixels(rect, colorData, 4, 0);

                // Update our pixel data cache
                _pixelData[x, y] = color;
            }
            finally
            {
                // Always unlock the bitmap
                _bitmap.Unlock();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Pixel error: {ex.Message}";
        }
    }

    private void FloodFill(int x, int y, Color targetColor, Color replacementColor)
    {
        if (targetColor == replacementColor) return;
        if (_pixelData == null || _bitmap == null) return;

        var queue = new Queue<Point>();
        queue.Enqueue(new Point(x, y));

        _bitmap.Lock();
        try
        {
            while (queue.Count > 0)
            {
                var point = queue.Dequeue();
                int px = (int)point.X;
                int py = (int)point.Y;

                if (px < 0 || px >= ImageWidth || py < 0 || py >= ImageHeight)
                    continue;

                if (_pixelData[px, py] != targetColor)
                    continue;

                _pixelData[px, py] = replacementColor;
                var colorData = new byte[] { replacementColor.B, replacementColor.G, replacementColor.R, replacementColor.A };
                Int32Rect rect = new Int32Rect(px, py, 1, 1);
                _bitmap.WritePixels(rect, colorData, 4, 0);

                queue.Enqueue(new Point(px - 1, py));
                queue.Enqueue(new Point(px + 1, py));
                queue.Enqueue(new Point(px, py - 1));
                queue.Enqueue(new Point(px, py + 1));
            }
        }
        finally
        {
            _bitmap.Unlock();
        }
    }

    public void UpdateCursorPosition(int x, int y)
    {
        StatusText = $"X: {x / ZoomLevel}, Y: {y / ZoomLevel}";

        // Update hover position
        if (x >= 0 && x < ImageWidth && y >= 0 && y < ImageHeight)
        {
            HoverX = x;
            HoverY = y;
        }
        else
        {
            HoverX = -1;
            HoverY = -1;
        }
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

            // --- Notify ResourcesPage to refresh textures ---
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is Modrix.Views.Windows.ProjectWorkspace ws)
                    {
                        foreach (var obj in LogicalTreeHelper.GetChildren(ws))
                        {
                            if (obj is Frame frame && frame.Content is Modrix.Views.Pages.ResourcesPage page)
                            {
                                page.Refresh();
                            }
                        }
                    }
                }
            });
            // --- End notify ---
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
        }
    }

    private bool CanUndo() => _undoStack.Any();

    private void Undo()
    {
        if (_undoStack.Any() && _pixelData != null)
        {
            var previousState = _undoStack.Pop();
            _redoStack.Push(ClonePixelData(_pixelData)!);

            ApplyPixelData(previousState);

            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
            HasUnsavedChanges = _undoStack.Any();
            StatusText = "Undo performed";
        }
    }

    private bool CanRedo() => _redoStack.Any();

    private void Redo()
    {
        if (_redoStack.Any() && _pixelData != null)
        {
            var nextState = _redoStack.Pop();
            _undoStack.Push(ClonePixelData(_pixelData)!);

            ApplyPixelData(nextState);

            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
            HasUnsavedChanges = true;
            StatusText = "Redo performed";
        }
    }

    private void ApplyPixelData(Color[,] newPixelData)
    {
        if (_bitmap == null || newPixelData == null) return;

        _pixelData = newPixelData;

        _bitmap.Lock();
        try
        {
            for (int y = 0; y < ImageHeight; y++)
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    var color = _pixelData[x, y];
                    var colorData = new byte[] { color.B, color.G, color.R, color.A };
                    Int32Rect rect = new Int32Rect(x, y, 1, 1);
                    _bitmap.WritePixels(rect, colorData, 4, 0);
                }
            }
        }
        finally
        {
            _bitmap.Unlock();
        }
        OnPropertyChanged(nameof(CurrentImage));
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

    partial void OnCurrentToolChanged(EditorTool value)
    {
        switch (value)
        {
            case EditorTool.Pencil:
                CurrentCursor = PencilCursor;
                break;
            case EditorTool.Eraser:
                CurrentCursor = EraserCursor ?? Cursors.Cross;
                break;
            case EditorTool.Bucket:
                CurrentCursor = BucketCursor ?? Cursors.Hand;
                break;
            case EditorTool.Picker:
                CurrentCursor = PickerCursor ?? Cursors.IBeam;
                break;
            default:
                CurrentCursor = DefaultCursor;
                break;
        }
    }
}