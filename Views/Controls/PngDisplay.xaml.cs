using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Modrix.Views.Controls
{
    public partial class PngDisplay : UserControl, INotifyPropertyChanged, IDisposable
    {
        public static readonly DependencyProperty SourcePathProperty = DependencyProperty.Register(
            nameof(SourcePath), typeof(string), typeof(PngDisplay), new PropertyMetadata(null, OnSourceChanged));

        public static readonly DependencyProperty IsAnimatedProperty = DependencyProperty.Register(
            nameof(IsAnimated), typeof(bool), typeof(PngDisplay), new PropertyMetadata(true));

        public static readonly DependencyProperty AnimationEnabledProperty = DependencyProperty.Register(
            nameof(AnimationEnabled), typeof(bool), typeof(PngDisplay), new PropertyMetadata(true, OnAnimationEnabledChanged));

        public event PropertyChangedEventHandler? PropertyChanged;
        private FileSystemWatcher? _watcher;
        private System.Timers.Timer? _frameTimer; // explicitly use System.Timers.Timer
        private BitmapSource[] _frames = Array.Empty<BitmapSource>();
        private int[] _durations = Array.Empty<int>();
        private int _currentFrameIndex = 0;
        private DateTime _lastFrameSwitch = DateTime.UtcNow;
        private string? _mcmetaPath;

        public string? SourcePath
        {
            get => (string?)GetValue(SourcePathProperty);
            set => SetValue(SourcePathProperty, value);
        }

        public bool IsAnimated
        {
            get => (bool)GetValue(IsAnimatedProperty);
            set => SetValue(IsAnimatedProperty, value);
        }

        public bool AnimationEnabled
        {
            get => (bool)GetValue(AnimationEnabledProperty);
            set => SetValue(AnimationEnabledProperty, value);
        }

        public PngDisplay()
        {
            InitializeComponent();
            Loaded += PngDisplay_Loaded;
            Unloaded += PngDisplay_Unloaded;
        }

        private void PngDisplay_Loaded(object sender, RoutedEventArgs e)
        {
            StartWatcher();
            LoadImage();
            SetupTimer();
        }

        private void PngDisplay_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PngDisplay pd)
            {
                pd.RestartWatcher();
                pd.LoadImage();
            }
        }

        private static void OnAnimationEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PngDisplay pd)
            {
                pd.SetupTimer();
            }
        }

        private void RestartWatcher()
        {
            StopWatcher();
            StartWatcher();
        }

        private void StartWatcher()
        {
            if (string.IsNullOrEmpty(SourcePath) || !File.Exists(SourcePath)) return;
            try
            {
                var dir = Path.GetDirectoryName(SourcePath)!;
                var file = Path.GetFileName(SourcePath);
                _watcher = new FileSystemWatcher(dir, file + "*"); // include mcmeta
                _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
                _watcher.Changed += Watcher_Changed;
                _watcher.Created += Watcher_Changed;
                _watcher.Renamed += Watcher_Changed;
                _watcher.Deleted += Watcher_Changed;
                _watcher.EnableRaisingEvents = true;

                _mcmetaPath = SourcePath + ".mcmeta";
                if (File.Exists(_mcmetaPath))
                {
                    var mcFile = Path.GetFileName(_mcmetaPath);
                    var mcWatcher = new FileSystemWatcher(dir, mcFile);
                }
            }
            catch { }
        }

        private void StopWatcher()
        {
            if (_watcher != null)
            {
                try
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                }
                catch { }
                _watcher = null;
            }
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            // Debounce slight delays
            Dispatcher.InvokeAsync(async () =>
            {
                await System.Threading.Tasks.Task.Delay(50);
                LoadImage();
            });
        }

        private void LoadImage()
        {
            if (string.IsNullOrEmpty(SourcePath) || !File.Exists(SourcePath))
            {
                ImageHost.Source = null;
                _frames = Array.Empty<BitmapSource>();
                _durations = Array.Empty<int>();
                return;
            }
            try
            {
                using var fs = new FileStream(SourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = fs;
                bmp.EndInit();
                bmp.Freeze();

                bool spriteSheet = bmp.PixelHeight > bmp.PixelWidth && bmp.PixelHeight % bmp.PixelWidth == 0;
                _mcmetaPath = SourcePath + ".mcmeta";
                if (spriteSheet)
                {
                    BuildFramesFromSpriteSheet(bmp);
                }
                else
                {
                    _frames = new BitmapSource[] { bmp };
                    _durations = new int[] { 1000 }; // static
                    _currentFrameIndex = 0;
                }
                ImageHost.Source = _frames.Length > 0 ? _frames[0] : bmp;
                SetupTimer();
            }
            catch { }
        }

        private void BuildFramesFromSpriteSheet(BitmapImage sheet)
        {
            int frameSize = sheet.PixelWidth;
            int frameCount = sheet.PixelHeight / frameSize;
            var frames = new BitmapSource[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                try
                {
                    var crop = new CroppedBitmap(sheet, new System.Windows.Int32Rect(0, i * frameSize, frameSize, frameSize));
                    crop.Freeze();
                    frames[i] = crop;
                }
                catch { }
            }
            _frames = frames.Where(f => f != null).ToArray();
            _durations = Enumerable.Repeat(100, _frames.Length).ToArray();
            // Parse mcmeta for custom durations
            try
            {
                if (_mcmetaPath != null && File.Exists(_mcmetaPath))
                {
                    var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(_mcmetaPath));
                    if (json.RootElement.TryGetProperty("animation", out var anim))
                    {
                        int globalTicks = anim.TryGetProperty("frametime", out var ft) ? ft.GetInt32() : 1;
                        int globalMs = Math.Max(1, globalTicks) * 50;
                        if (anim.TryGetProperty("frames", out var framesElem) && framesElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            var seqDur = _durations.ToList();
                            for (int i = 0; i < framesElem.GetArrayLength() && i < seqDur.Count; i++)
                            {
                                var f = framesElem[i];
                                if (f.ValueKind == System.Text.Json.JsonValueKind.Number)
                                {
                                    seqDur[i] = globalMs;
                                }
                                else if (f.ValueKind == System.Text.Json.JsonValueKind.Object)
                                {
                                    if (f.TryGetProperty("time", out var tprop))
                                    {
                                        int ticks = tprop.GetInt32();
                                        seqDur[i] = Math.Max(1, ticks) * 50;
                                    }
                                    else
                                    {
                                        seqDur[i] = globalMs;
                                    }
                                }
                            }
                            _durations = seqDur.ToArray();
                        }
                        else
                        {
                            _durations = Enumerable.Repeat(globalMs, _frames.Length).ToArray();
                        }
                    }
                }
            }
            catch { }
            _currentFrameIndex = 0;
        }

        private void SetupTimer()
        {
            if (_frameTimer != null)
            {
                _frameTimer.Stop();
                _frameTimer.Elapsed -= FrameTimer_Elapsed;
                _frameTimer.Dispose();
                _frameTimer = null;
            }
            if (!AnimationEnabled || _frames.Length <= 1)
            {
                return; // no animation needed
            }
            _frameTimer = new System.Timers.Timer(50);
            _frameTimer.Elapsed += FrameTimer_Elapsed;
            _frameTimer.AutoReset = true;
            _frameTimer.Start();
        }

        private void FrameTimer_Elapsed(object? sender, global::System.Timers.ElapsedEventArgs e)
        {
            if (_frames.Length <= 1) return;
            var now = DateTime.UtcNow;
            int currentDuration = _durations.Length > _currentFrameIndex ? _durations[_currentFrameIndex] : 100;
            if ((now - _lastFrameSwitch).TotalMilliseconds >= currentDuration)
            {
                _currentFrameIndex = (_currentFrameIndex + 1) % _frames.Length;
                _lastFrameSwitch = now;
                Dispatcher.Invoke(() =>
                {
                    try { ImageHost.Source = _frames[_currentFrameIndex]; } catch { }
                });
            }
        }

        protected virtual void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void Dispose()
        {
            StopWatcher();
            if (_frameTimer != null)
            {
                try { _frameTimer.Stop(); _frameTimer.Dispose(); } catch { }
                _frameTimer = null;
            }
        }
    }
}
