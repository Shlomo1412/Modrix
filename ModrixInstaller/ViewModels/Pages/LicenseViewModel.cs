using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ModrixInstaller.ViewModels.Pages;

public class LicenseViewModel : ObservableObject
{
    private string _licenseText = string.Empty;
    public string LicenseText
    {
        get => _licenseText;
        set => SetProperty(ref _licenseText, value);
    }

    private bool _isAccepted;
    public bool IsAccepted
    {
        get => _isAccepted;
        set => SetProperty(ref _isAccepted, value);
    }

    private const string RequiredHeader = "MIT License\n\nCopyright (c) 2025 Modrix";

    public LicenseViewModel()
    {
        LoadLicense();
    }

    private void LoadLicense()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var possiblePaths = new []
            {
                Path.Combine(baseDir, "LICENSE"),
                Path.Combine(baseDir, "..", "..", "..", "LICENSE"),
                Path.Combine(Directory.GetCurrentDirectory(), "LICENSE"),
                Path.Combine(AppContext.BaseDirectory, "ModrixInstaller", "LICENSE")
            };
            foreach (var p in possiblePaths)
            {
                if (File.Exists(p))
                {
                    var text = File.ReadAllText(p);
                    if (!text.StartsWith(RequiredHeader, StringComparison.OrdinalIgnoreCase))
                    {
                        // Prepend required header if custom license missing it
                        text = RequiredHeader + "\n\n" + text.Trim();
                    }
                    LicenseText = text;
                    return;
                }
            }
            LicenseText = RequiredHeader + "\n\n(Original LICENSE file not found in expected locations.)";
        }
        catch (Exception ex)
        {
            LicenseText = RequiredHeader + $"\n\nFailed to load license: {ex.Message}";
        }
    }
}
