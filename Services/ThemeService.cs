using System;
using System.IO;
using System.Text.Json;
using Modrix.Models;
using Wpf.Ui.Appearance;

namespace Modrix.Services
{
    public interface IThemeService
    {
        ApplicationTheme LoadTheme();
        void SaveTheme(ApplicationTheme theme);
    }

    public class ThemeService : IThemeService
    {
        private readonly string _configPath;
        private AppConfig _config;

        public ThemeService()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Modrix"
            );
            Directory.CreateDirectory(folder);

            _configPath = Path.Combine(folder, "appsettings.json");

            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _config = JsonSerializer.Deserialize<AppConfig>(json)
                          ?? new AppConfig();
            }
            else
            {
                _config = new AppConfig();
                SaveConfig();
            }
        }

        public ApplicationTheme LoadTheme()
        {
            return Enum.TryParse<ApplicationTheme>(_config.Theme, out var t)
                ? t
                : ApplicationTheme.Dark;
        }

        public void SaveTheme(ApplicationTheme theme)
        {
            _config.Theme = theme.ToString();
            SaveConfig();
        }

        private void SaveConfig()
        {
            var json = JsonSerializer.Serialize(_config,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
    }
}
