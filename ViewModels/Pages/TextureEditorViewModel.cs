using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Abstractions.Controls;
using System.Windows.Controls; // <-- Add this for Frame
using System.Windows.Threading;

namespace Modrix.ViewModels.Pages;

public enum EditorTool { Pencil, Eraser, Bucket, Picker, Line, Rectangle }

public class AnimationFrame : ObservableObject
{
    private WriteableBitmap _bitmap;
    public WriteableBitmap Bitmap
    {
        get => _bitmap;
        set => SetProperty(ref _bitmap, value);
    }

    private int _durationMs = 100; // default 100ms
    public int DurationMs
    {
        get => _durationMs;
        set => SetProperty(ref _durationMs, Math.Max(10, value));
    }

    public BitmapSource Thumbnail
    {
        get
        {
            try
            {
                var scale = 40.0 / Math.Max(Bitmap.PixelWidth, Bitmap.PixelHeight);
                var tb = new TransformedBitmap(Bitmap, new ScaleTransform(scale, scale));
                tb.Freeze();
                return tb;
            }
            catch { return Bitmap; }
        }
    }
}

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

    // Animation related
    // Removed ObservableProperty attributes and implement manually to avoid generator issues
    private bool _allowAnimation = false;
    public bool AllowAnimation
    {
        get => _allowAnimation;
        private set
        {
            if (SetProperty(ref _allowAnimation, value))
            {
                UpdateAnimationEnabled();
            }
        }
    }

    private bool _isAnimationEnabled = false;
    public bool IsAnimationEnabled
    {
        get => _isAnimationEnabled;
        private set => SetProperty(ref _isAnimationEnabled, value);
    }

    private int _currentFrameIndex = 0;
    public int CurrentFrameIndex
    {
        get => _currentFrameIndex;
        set
        {
            if (value < 0) value = 0;
            if (value >= Frames.Count) value = Frames.Count - 1;
            if (SetProperty(ref _currentFrameIndex, value))
            {
                SwitchToFrame(value);
                StatusText = $"Frame {value + 1}/{Frames.Count}";
                OnPropertyChanged(nameof(DisplayCurrentFrame));
            }
        }
    }

    private bool _isPlaying = false;
    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetProperty(ref _isPlaying, value);
    }

    private DispatcherTimer? _playbackTimer;

    public ObservableCollection<AnimationFrame> Frames { get; } = new();

    public int FrameCount => Frames.Count; // for binding instead of Frames.Count
    public int DisplayCurrentFrame => CurrentFrameIndex + 1; // 1-based for UI

    private WriteableBitmap? _bitmap; // current frame bitmap
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
        SelectLineCommand = new RelayCommand(() => CurrentTool = EditorTool.Line);
        SelectRectangleCommand = new RelayCommand(() => CurrentTool = EditorTool.Rectangle);
        ZoomInCommand = new RelayCommand(ZoomIn);
        ZoomOutCommand = new RelayCommand(ZoomOut);
        ToggleGridCommand = new RelayCommand(() => ShowGrid = !ShowGrid);
        ClearCommand = new RelayCommand(ClearCanvas, () => _bitmap != null);
        UndoCommand = new RelayCommand(Undo, CanUndo);
        RedoCommand = new RelayCommand(Redo, CanRedo);
        AddFrameCommand = new RelayCommand(AddFrame, () => AllowAnimation);
        RemoveFrameCommand = new RelayCommand(RemoveCurrentFrame, () => AllowAnimation && Frames.Count > 1);
        NextFrameCommand = new RelayCommand(NextFrame, () => AllowAnimation && Frames.Count > 1);
        PreviousFrameCommand = new RelayCommand(PreviousFrame, () => AllowAnimation && Frames.Count > 1);
        PlayPauseCommand = new RelayCommand(TogglePlay, () => AllowAnimation && Frames.Count > 1);

        _playbackTimer = new DispatcherTimer();
        _playbackTimer.Tick += PlaybackTimer_Tick;

        Frames.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(FrameCount));
            OnPropertyChanged(nameof(DisplayCurrentFrame));
        };
    }

    public IAsyncRelayCommand SaveCommand { get; }
    public ICommand SelectPencilCommand { get; }
    public ICommand SelectEraserCommand { get; }
    public ICommand SelectBucketCommand { get; }
    public ICommand SelectPickerCommand { get; }
    public ICommand SelectLineCommand { get; }
    public ICommand SelectRectangleCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ToggleGridCommand { get; }
    public ICommand ClearCommand { get; }
    public IRelayCommand UndoCommand { get; }
    public IRelayCommand RedoCommand { get; }
    public IRelayCommand AddFrameCommand { get; }
    public IRelayCommand RemoveFrameCommand { get; }
    public IRelayCommand NextFrameCommand { get; }
    public IRelayCommand PreviousFrameCommand { get; }
    public IRelayCommand PlayPauseCommand { get; }

    public void EnableAnimation(bool allow)
    {
        AllowAnimation = allow;
        (AddFrameCommand as RelayCommand)?.NotifyCanExecuteChanged();
        UpdateAnimationEnabled();
    }

    private void UpdateAnimationEnabled()
    {
        IsAnimationEnabled = AllowAnimation && Frames.Count > 0;
        (RemoveFrameCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (NextFrameCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (PreviousFrameCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (PlayPauseCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsPlaying || Frames.Count == 0) return;
        // Advance based on current frame duration
        if (CurrentFrameIndex < 0 || CurrentFrameIndex >= Frames.Count) CurrentFrameIndex = 0;
        var frame = Frames[CurrentFrameIndex];
        _playbackTimer!.Interval = TimeSpan.FromMilliseconds(Math.Max(10, frame.DurationMs));
        NextFrame();
    }

    private void TogglePlay()
    {
        if (!IsPlaying)
        {
            IsPlaying = true;
            StatusText = "Playing animation";
            _playbackTimer!.Interval = TimeSpan.FromMilliseconds(Math.Max(10, Frames[CurrentFrameIndex].DurationMs));
            _playbackTimer.Start();
        }
        else
        {
            IsPlaying = false;
            StatusText = "Stopped";
            _playbackTimer!.Stop();
        }
    }

    private void AddFrame()
    {
        if (!AllowAnimation) return;
        // Clone current frame or create blank
        WriteableBitmap newBmp;
        if (_bitmap != null)
        {
            newBmp = CloneWriteableBitmap(_bitmap);
        }
        else
        {
            newBmp = new WriteableBitmap(ImageWidth, ImageHeight, 96, 96, PixelFormats.Bgra32, null);
        }
        var frame = new AnimationFrame { Bitmap = newBmp, DurationMs = 100 };
        Frames.Add(frame);
        CurrentFrameIndex = Frames.Count - 1;
        SwitchToFrame(CurrentFrameIndex);
        UpdateAnimationEnabled();
    }

    private WriteableBitmap CloneWriteableBitmap(WriteableBitmap source)
    {
        var clone = new WriteableBitmap(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY, source.Format, null);
        var stride = source.BackBufferStride;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        clone.Lock();
        try { clone.WritePixels(new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight), pixels, stride, 0); }
        finally { clone.Unlock(); }
        return clone;
    }

    private void RemoveCurrentFrame()
    {
        if (Frames.Count <= 1) return;
        var idx = CurrentFrameIndex;
        Frames.RemoveAt(idx);
        if (idx >= Frames.Count) idx = Frames.Count - 1;
        CurrentFrameIndex = idx;
        SwitchToFrame(CurrentFrameIndex);
        UpdateAnimationEnabled();
        HasUnsavedChanges = true;
    }

    private void NextFrame()
    {
        if (Frames.Count == 0) return;
        var next = (CurrentFrameIndex + 1) % Frames.Count;
        CurrentFrameIndex = next;
        SwitchToFrame(CurrentFrameIndex);
    }

    private void PreviousFrame()
    {
        if (Frames.Count == 0) return;
        var prev = (CurrentFrameIndex - 1 + Frames.Count) % Frames.Count;
        CurrentFrameIndex = prev;
        SwitchToFrame(CurrentFrameIndex);
    }

    private void SwitchToFrame(int index)
    {
        if (index < 0 || index >= Frames.Count) return;
        // Save current pixel data back into current frame bitmap first (already applied live)
        var frame = Frames[index];
        _bitmap = frame.Bitmap;
        if (_bitmap.IsFrozen)
        {
            _bitmap = CloneWriteableBitmap(_bitmap);
            frame.Bitmap = _bitmap;
        }
        ImageWidth = _bitmap.PixelWidth;
        ImageHeight = _bitmap.PixelHeight;
        UpdatePixelData();
        CurrentImage = _bitmap;
        StatusText = $"Frame {index + 1}/{Frames.Count}";
    }

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

        try
        {
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.UriSource = new Uri(PngPath);
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            // Ensure BGRA32 format
            BitmapSource src = bitmapImage;
            if (bitmapImage.Format != PixelFormats.Bgra32)
            {
                var converted = new FormatConvertedBitmap(bitmapImage, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                src = converted;
            }

            FileName = Path.GetFileName(PngPath);
            HasUnsavedChanges = false;

            Frames.Clear();

            // Attempt to parse .mcmeta if present
            var mcmetaPath = PngPath + ".mcmeta";
            List<int>? frameDurations = null;
            if (File.Exists(mcmetaPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(mcmetaPath));
                    if (doc.RootElement.TryGetProperty("animation", out var anim))
                    {
                        int globalTime = anim.TryGetProperty("frametime", out var ft) ? ft.GetInt32() : 1;
                        if (anim.TryGetProperty("frames", out var framesElem) && framesElem.ValueKind == JsonValueKind.Array)
                        {
                            frameDurations = new();
                            foreach (var f in framesElem.EnumerateArray())
                            {
                                if (f.ValueKind == JsonValueKind.Number)
                                {
                                    frameDurations.Add(globalTime * 50); // convert ticks (50ms)
                                }
                                else if (f.ValueKind == JsonValueKind.Object && f.TryGetProperty("index", out var idxProp))
                                {
                                    int time = f.TryGetProperty("time", out var tprop) ? tprop.GetInt32() : globalTime;
                                    frameDurations.Add(time * 50);
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // Detect sprite sheet (vertical) if height multiple of width
            if (AllowAnimation && src.PixelHeight > src.PixelWidth && src.PixelHeight % src.PixelWidth == 0)
            {
                int frameSize = src.PixelWidth; // square frames expected
                int frameCount = src.PixelHeight / frameSize;
                for (int i = 0; i < frameCount; i++)
                {
                    var wb = new WriteableBitmap(frameSize, frameSize, 96, 96, PixelFormats.Bgra32, null);
                    var rect = new Int32Rect(0, i * frameSize, frameSize, frameSize);
                    int bpp = (src.Format.BitsPerPixel + 7) / 8;
                    int stride = frameSize * bpp;
                    byte[] pixels = new byte[stride * frameSize];
                    src.CopyPixels(rect, pixels, stride, 0);
                    wb.Lock();
                    try { wb.WritePixels(new Int32Rect(0, 0, frameSize, frameSize), pixels, stride, 0); }
                    finally { wb.Unlock(); }
                    Frames.Add(new AnimationFrame { Bitmap = wb, DurationMs = frameDurations != null && i < frameDurations.Count ? frameDurations[i] : 100 });
                }
                if (Frames.Count == 0)
                {
                    var single = new WriteableBitmap(src);
                    Frames.Add(new AnimationFrame { Bitmap = single, DurationMs = 100 });
                }
                CurrentFrameIndex = 0;
                SwitchToFrame(0);
            }
            else
            {
                // Single frame
                var wbSingle = new WriteableBitmap(src);
                Frames.Add(new AnimationFrame { Bitmap = wbSingle, DurationMs = 100 });
                CurrentFrameIndex = 0;
                SwitchToFrame(0);
            }

            UpdateAnimationEnabled();
            _undoStack.Clear();
            _redoStack.Clear();
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
            (ClearCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusText = $"Load error: {ex.Message}";
        }
    }

    private void UpdatePixelData()
    {
        if (_bitmap == null) return;

        try
        {
            // Guarantee BGRA32 pixel format for predictable layout
            BitmapSource src = _bitmap;
            if (_bitmap.Format != PixelFormats.Bgra32)
            {
                var converted = new FormatConvertedBitmap(_bitmap, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                src = converted;
            }

            int bytesPerPixel = (src.Format.BitsPerPixel + 7) / 8; // should be 4
            int stride = src.PixelWidth * bytesPerPixel;
            var raw = new byte[stride * src.PixelHeight];
            src.CopyPixels(raw, stride, 0);

            _pixelData = new Color[src.PixelWidth, src.PixelHeight];

            for (int y = 0; y < src.PixelHeight; y++)
            {
                int rowStart = y * stride;
                for (int x = 0; x < src.PixelWidth; x++)
                {
                    int index = rowStart + x * bytesPerPixel;
                    if (index + 3 >= raw.Length) // safety guard
                        continue;
                    byte b = raw[index];
                    byte g = raw[index + 1];
                    byte r = raw[index + 2];
                    byte a = raw[index + 3];
                    _pixelData[x, y] = Color.FromArgb(a, r, g, b);
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Pixel decode error: {ex.Message}";
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
            if (CurrentTool != EditorTool.Picker && CurrentTool != EditorTool.Pencil && CurrentTool != EditorTool.Line && CurrentTool != EditorTool.Rectangle)
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
                // Line & Rectangle handled separately (on mouse up) in code-behind
            }
            // HasUnsavedChanges is now set in PushUndoState
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    private void EnsureWritable()
    {
        if (_bitmap != null && _bitmap.IsFrozen)
        {
            _bitmap = CloneWriteableBitmap(_bitmap);
            if (AllowAnimation && CurrentFrameIndex >= 0 && CurrentFrameIndex < Frames.Count)
            {
                Frames[CurrentFrameIndex].Bitmap = _bitmap;
            }
            CurrentImage = _bitmap;
        }
    }

    private void SetPixel(int x, int y, Color color)
    {
        if (_bitmap == null || _pixelData == null) return;
        if (_pixelData[x, y] == color) return; // No change if color is the same
        EnsureWritable();
        try
        {
            var colorData = new byte[] { color.B, color.G, color.R, color.A };
            _bitmap.Lock();
            try
            {
                Int32Rect rect = new Int32Rect(x, y, 1, 1);
                _bitmap.WritePixels(rect, colorData, 4, 0);
                _pixelData[x, y] = color;
            }
            finally
            {
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
        EnsureWritable();
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

    // Drawing algorithms for shapes
    public void DrawLine(int x0, int y0, int x1, int y1, Color color)
    {
        if (_bitmap == null || _pixelData == null) return;
        EnsureWritable();
        _bitmap.Lock();
        try
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                if (x0 >= 0 && x0 < ImageWidth && y0 >= 0 && y0 < ImageHeight)
                {
                    _pixelData[x0, y0] = color;
                    var colorData = new byte[] { color.B, color.G, color.R, color.A };
                    _bitmap.WritePixels(new Int32Rect(x0, y0, 1, 1), colorData, 4, 0);
                }
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }
        finally
        {
            _bitmap.Unlock();
        }
    }

    public void DrawFilledRectangle(int x0, int y0, int x1, int y1, Color color)
    {
        if (_bitmap == null || _pixelData == null) return;
        if (x0 > x1) (x0, x1) = (x1, x0);
        if (y0 > y1) (y0, y1) = (y1, y0);
        x0 = Math.Max(0, x0); y0 = Math.Max(0, y0);
        x1 = Math.Min(ImageWidth - 1, x1); y1 = Math.Min(ImageHeight - 1, y1);
        EnsureWritable();
        _bitmap.Lock();
        try
        {
            var colorData = new byte[] { color.B, color.G, color.R, color.A };
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    _pixelData[x, y] = color;
                    _bitmap.WritePixels(new Int32Rect(x, y, 1, 1), colorData, 4, 0);
                }
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
        if (_bitmap == null)
            return;

        try
        {
            if (AllowAnimation && Frames.Count > 1)
            {
                // Build vertical sprite sheet
                int frameW = Frames[0].Bitmap.PixelWidth;
                int frameH = Frames[0].Bitmap.PixelHeight;
                int totalH = frameH * Frames.Count;
                var sheet = new WriteableBitmap(frameW, totalH, 96, 96, PixelFormats.Bgra32, null);
                sheet.Lock();
                try
                {
                    int bpp = (sheet.Format.BitsPerPixel + 7) / 8;
                    for (int i = 0; i < Frames.Count; i++)
                    {
                        var f = Frames[i].Bitmap;
                        int stride = f.BackBufferStride;
                        var pixels = new byte[stride * f.PixelHeight];
                        f.CopyPixels(pixels, stride, 0);
                        sheet.WritePixels(new Int32Rect(0, i * frameH, frameW, frameH), pixels, stride, 0);
                    }
                }
                finally { sheet.Unlock(); }

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(sheet));
                using (var fileStream = File.Create(PngPath))
                {
                    encoder.Save(fileStream);
                }

                // Write .mcmeta
                var animation = new
                {
                    animation = new
                    {
                        frametime = 1, // global fallback
                        frames = Frames.Select((f, idx) =>
                        {
                            int ticks = Math.Max(1, f.DurationMs / 50); // convert ms to 50ms units
                            return ticks == 1 ? (object)idx : new { index = idx, time = ticks };
                        }).ToArray()
                    }
                };
                var json = JsonSerializer.Serialize(animation, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(PngPath + ".mcmeta", json);
            }
            else
            {
                // Normal single frame save
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(_bitmap));
                using (var fileStream = File.Create(PngPath))
                {
                    encoder.Save(fileStream);
                }
                // Remove any stale mcmeta
                var mcmeta = PngPath + ".mcmeta";
                if (File.Exists(mcmeta))
                {
                    try { File.Delete(mcmeta); } catch { }
                }
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
        EnsureWritable();
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
        CurrentImage = _bitmap;
    }

    private void ClearCanvas()
    {
        if (_bitmap == null || _pixelData == null) return;
        PushUndoState();
        EnsureWritable();
        _bitmap.Lock();
        try
        {
            var transparent = new byte[] { 0, 0, 0, 0 };
            for (int y = 0; y < ImageHeight; y++)
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    _pixelData[x, y] = Colors.Transparent;
                    _bitmap.WritePixels(new Int32Rect(x, y, 1, 1), transparent, 4, 0);
                }
            }
        }
        finally
        {
            _bitmap.Unlock();
        }
        StatusText = "Canvas cleared";
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
                StatusText = "Pencil";
                break;
            case EditorTool.Eraser:
                CurrentCursor = EraserCursor ?? Cursors.Cross;
                StatusText = "Eraser";
                break;
            case EditorTool.Bucket:
                CurrentCursor = BucketCursor ?? Cursors.Hand;
                StatusText = "Bucket";
                break;
            case EditorTool.Picker:
                CurrentCursor = PickerCursor ?? Cursors.IBeam;
                StatusText = "Color Picker";
                break;
            case EditorTool.Line:
                CurrentCursor = Cursors.Cross;
                StatusText = "Line";
                break;
            case EditorTool.Rectangle:
                CurrentCursor = Cursors.Cross;
                StatusText = "Rectangle";
                break;
            default:
                CurrentCursor = DefaultCursor;
                break;
        }
    }
}