using ModrixInstaller.Models;
using System.IO;

namespace ModrixInstaller.Services
{
    public class ConfigurationService
    {
        private InstallationConfiguration _configuration = new();

        public InstallationConfiguration Configuration => _configuration;

        public void UpdateConfiguration(Action<InstallationConfiguration> updateAction)
        {
            updateAction(_configuration);
        }

        public string GetDefaultInstallPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Modrix");
        }

        public bool IsValidInstallPath(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return false;

                // Check if path is valid
                Path.GetFullPath(path);

                // Check if we can write to the parent directory
                var parentDir = Path.GetDirectoryName(path);
                if (parentDir != null && Directory.Exists(parentDir))
                {
                    return HasWritePermission(parentDir);
                }

                return true; // Will be created during installation
            }
            catch
            {
                return false;
            }
        }

        private bool HasWritePermission(string path)
        {
            try
            {
                var testFile = Path.Combine(path, "test_write_permission.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public long GetRequiredDiskSpace()
        {
            // Estimated space for Modrix installation (in bytes)
            return 150 * 1024 * 1024; // 150 MB
        }

        public long GetAvailableDiskSpace(string path)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(path) ?? "C:");
                return drive.AvailableFreeSpace;
            }
            catch
            {
                return 0;
            }
        }

        public bool HasSufficientDiskSpace(string path)
        {
            return GetAvailableDiskSpace(path) >= GetRequiredDiskSpace();
        }
    }
}