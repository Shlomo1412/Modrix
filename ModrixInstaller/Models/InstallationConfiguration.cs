namespace ModrixInstaller.Models
{
    public class InstallationConfiguration
    {
        public string InstallPath { get; set; } = string.Empty;
        public bool CreateDesktopShortcut { get; set; } = true;
        public bool CreateStartMenuShortcut { get; set; } = true;
        public bool RunAfterInstall { get; set; } = true;
        public bool AcceptedLicense { get; set; } = false;
        public string SelectedLanguage { get; set; } = "English";
        public bool CheckForUpdates { get; set; } = true;
        public bool SendUsageStatistics { get; set; } = false;
    }
}