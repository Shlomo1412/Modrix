using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Wpf.Ui.Abstractions.Controls;
using System.Windows.Input;

namespace Modrix.ViewModels.Pages;

[ObservableObject]
public partial class TextureEditorViewModel : INavigationAware
{
    [ObservableProperty]
    private string _pngPath;

    [ObservableProperty]
    private BitmapImage _currentImage;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private int _imageWidth;

    [ObservableProperty]
    private int _imageHeight;

    [ObservableProperty]
    private string _fileName;

    public TextureEditorViewModel()
    {
        SaveCommand = new AsyncRelayCommand(SaveChangesAsync);
        _hasUnsavedChanges = false;
    }

    public IAsyncRelayCommand SaveCommand { get; }

    public void SetPngPath(string path)
    {
        PngPath = path;
        if (PngPath != null)
        {
            LoadImage();
        }
    }

    public Task OnNavigatedToAsync()
    {
        if (!string.IsNullOrEmpty(PngPath))
        {
            LoadImage();
        }
        return Task.CompletedTask;
    }

    public Task OnNavigatedFromAsync()
    {
        // Clean up resources if needed
        return Task.CompletedTask;
    }

    private void LoadImage()
    {
        if (!File.Exists(PngPath))
            return;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(PngPath);
        image.EndInit();

        CurrentImage = image;
        ImageWidth = image.PixelWidth;
        ImageHeight = image.PixelHeight;
        FileName = Path.GetFileName(PngPath);
        HasUnsavedChanges = false;
    }

    private async Task SaveChangesAsync()
    {
        if (!HasUnsavedChanges || CurrentImage == null)
            return;

        try
        {
            using (var fileStream = File.Create(PngPath))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(CurrentImage));
                encoder.Save(fileStream);
            }
            
            HasUnsavedChanges = false;
        }
        catch (Exception)
        {
            // Handle save errors
            throw;
        }
    }

    public void ApplyImageChanges(BitmapSource newImage)
    {
        if (newImage == null)
            return;

        var bitmap = new BitmapImage();
        using (var memoryStream = new MemoryStream())
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(newImage));
            encoder.Save(memoryStream);
            memoryStream.Position = 0;

            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = memoryStream;
            bitmap.EndInit();
            bitmap.Freeze();
        }

        CurrentImage = bitmap;
        HasUnsavedChanges = true;
    }
}