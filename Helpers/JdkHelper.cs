using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

public class JdkHelper
{

    public class JdkInfo
    {
        public string Path { get; set; }
        public string Source { get; set; } // "Modrix JDKs", "JAVA_HOME", etc.
        public string Version { get; set; }
        public bool IsRemovable { get; set; }
    }

    public List<int> GetAvailableJdkVersions()
    {
        return new List<int> { 8, 17, 21 };
    }

    /// <summary>
    /// Ensures that a JDK of at least the required major version is available.
    /// If none is found locally, prompts the user to download/install it.
    /// </summary>
    public async Task EnsureRequiredJdk(string minecraftVersion, IProgress<(string, int)> progress)
    {
        int requiredJava = GetRequiredJavaVersion(minecraftVersion);
        var jdkHome = FindBestJdkHome(requiredJava);

        if (jdkHome == null)
        {
            // Prompt user to download JDK
            bool download = await ShowDownloadDialogAsync(requiredJava);
            if (!download)
                throw new OperationCanceledException($"Java {requiredJava} is required but not installed");

            jdkHome = await DownloadAndInstallJdkAsync(requiredJava, progress);
            if (jdkHome == null)
                throw new Exception($"Failed to install JDK {requiredJava}");
        }
        else
        {
            Debug.WriteLine($"[JDK] Found suitable JDK at {jdkHome}");
        }

        // At this point you can set JAVA_HOME or pass jdkHome to your Gradle invocation
        Environment.SetEnvironmentVariable("JAVA_HOME", jdkHome);
    }

    public List<JdkInfo> GetInstalledJdks()
    {
        var jdks = new List<JdkInfo>();

        // 1. Check Modrix JDKs directory
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var modrixJdksRoot = Path.Combine(localAppData, "Modrix", "JDKs");
        if (Directory.Exists(modrixJdksRoot))
        {
            foreach (var dir in Directory.GetDirectories(modrixJdksRoot))
            {
                if (IsValidJdk(dir))
                {
                    jdks.Add(new JdkInfo
                    {
                        Path = dir,
                        Source = "Modrix JDKs",
                        Version = GetJavaVersion(dir),
                        IsRemovable = true
                    });
                }
            }
        }

        // 2. Check user's .jdks folder
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userJdksPath = Path.Combine(userProfile, ".jdks");
        if (Directory.Exists(userJdksPath))
        {
            foreach (var dir in Directory.GetDirectories(userJdksPath))
            {
                if (IsValidJdk(dir))
                {
                    jdks.Add(new JdkInfo
                    {
                        Path = dir,
                        Source = "User .jdks",
                        Version = GetJavaVersion(dir),
                        IsRemovable = false // Typically managed by IDEs
                    });
                }
            }
        }

        // 3. Check JAVA_HOME
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome) && Directory.Exists(javaHome) && IsValidJdk(javaHome))
        {
            jdks.Add(new JdkInfo
            {
                Path = javaHome,
                Source = "JAVA_HOME",
                Version = GetJavaVersion(javaHome),
                IsRemovable = false
            });
        }

        // 4. Check common installation paths
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var commonJdkPaths = new[]
        {
            Path.Combine(programFiles, "Java"),
            Path.Combine(programFiles, "Eclipse Foundation"),
            Path.Combine(programFiles, "AdoptOpenJDK"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Java")
        };

        foreach (var basePath in commonJdkPaths)
        {
            if (Directory.Exists(basePath))
            {
                foreach (var jdkDir in Directory.GetDirectories(basePath, "jdk*"))
                {
                    if (IsValidJdk(jdkDir))
                    {
                        jdks.Add(new JdkInfo
                        {
                            Path = jdkDir,
                            Source = "System Installation",
                            Version = GetJavaVersion(jdkDir),
                            IsRemovable = false
                        });
                    }
                }
            }
        }

        return jdks;
    }

    private string GetJavaVersion(string jdkPath)
    {
        var javaExe = Path.Combine(jdkPath, "bin", "java.exe");
        try
        {
            var startInfo = new ProcessStartInfo(javaExe, "-version")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit(2000);
                var versionOutput = process.StandardError.ReadToEnd();
                if (string.IsNullOrWhiteSpace(versionOutput))
                    versionOutput = process.StandardOutput.ReadToEnd();

                // Parse version from various output formats
                var match = Regex.Match(
                    versionOutput,
                    @"version\s+""?(\d+(\.\d+)*)([._]\d+)?",
                    RegexOptions.IgnoreCase
                );

                return match.Success ? match.Groups[1].Value : "Unknown";
            }
        }
        catch
        {
            return "Unknown";
        }
    }

    private bool IsValidJdk(string path)
    {
        var javaExe = Path.Combine(path, "bin", "java.exe");
        return File.Exists(javaExe);
    }

    /// <summary>
    /// Scans the Modrix\JDKs folder (and JAVA_HOME) for an installed JDK whose major version
    /// matches the requiredJava. Returns its path, or null if none found.
    /// </summary>
    private string? FindBestJdkHome(int requiredJava)
    {
        // 1) Look under %LocalAppData%\Modrix\JDKs\jdk-<requiredJava>*
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var jdkRoot = Path.Combine(localAppData, "Modrix", "JDKs");

        if (Directory.Exists(jdkRoot))
        {
            var candidates = Directory
                .GetDirectories(jdkRoot, $"jdk-{requiredJava}*")
                .Where(IsValidJdk)
                .OrderByDescending(d => d) // pick the highest sub-version first
                .ToList();

            if (candidates.Count > 0)
                return candidates[0];
        }

        // 2) Check user's .jdks folder
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userJdksPath = Path.Combine(userProfile, ".jdks");
        if (Directory.Exists(userJdksPath))
        {
            var candidates = Directory
                .GetDirectories(userJdksPath, $"*jdk-{requiredJava}*")
                .Where(IsValidJdk)
                .OrderByDescending(d => d)
                .ToList();

            if (candidates.Count > 0)
                return candidates[0];
        }

        // 3) Fall back to system JAVA_HOME if it matches
        var sysJavaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(sysJavaHome) && Directory.Exists(sysJavaHome) && IsValidJdk(sysJavaHome))
        {
            try
            {
                var version = GetJavaVersion(sysJavaHome);
                if (!string.IsNullOrEmpty(version) && version.StartsWith(requiredJava.ToString()))
                {
                    return sysJavaHome;
                }
            }
            catch
            {
                // ignore parse errors
            }
        }

        // 4) Check common installation paths
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var commonJdkPaths = new[]
        {
            Path.Combine(programFiles, "Java"),
            Path.Combine(programFiles, "Eclipse Foundation"),
            Path.Combine(programFiles, "AdoptOpenJDK"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Java")
        };

        foreach (var basePath in commonJdkPaths)
        {
            if (Directory.Exists(basePath))
            {
                var candidates = Directory
                    .GetDirectories(basePath, $"*jdk*{requiredJava}*")
                    .Where(IsValidJdk)
                    .OrderByDescending(d => d)
                    .ToList();

                if (candidates.Count > 0)
                    return candidates[0];
            }
        }

        return null;
    }

    private int GetRequiredJavaVersion(string minecraftVersion)
    {
        if (string.IsNullOrEmpty(minecraftVersion)) return 17;

        // Parse version (e.g., "1.21.4")
        var parts = minecraftVersion.Split('.');
        if (parts.Length < 2) return 17;

        if (!int.TryParse(parts[1], out int minor)) return 17;

        // Version mapping:
        return minor >= 21 ? 21 :
               minor >= 17 ? 17 : 8;
    }

    private Task<bool> ShowDownloadDialogAsync(int requiredVersion)
    {
        
        var msg = new Wpf.Ui.Controls.MessageBox()
        {
            Title = "Java JDK Required",
            Content = $"This project requires Java {requiredVersion} which is not installed.\n\n" +
                      "Would you like to download and install it automatically?",
            PrimaryButtonText = "Yes, install",
            CloseButtonText = "No",

            PrimaryButtonIcon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.ArrowDownload24 },
            CloseButtonIcon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Dismiss24 }

        };

        return msg.ShowDialogAsync()
                  .ContinueWith(t => t.Result == Wpf.Ui.Controls.MessageBoxResult.Primary);
    }

    public async Task<string> DownloadAndInstallJdkAsync(int version, IProgress<(string, int)> progress)
    {
        try
        {
            progress.Report(($"Starting download of JDK {version}...", 0));

            // Create persistent directory for JDK
            var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var jdkRoot = Path.Combine(appDataDir, "Modrix", "JDKs");
            Directory.CreateDirectory(jdkRoot);

            // Download JDK
            var jdkUrl = version switch
            {
                21 => "https://mirrors.huaweicloud.com/openjdk/21.0.2/openjdk-21.0.2_windows-x64_bin.zip",
                17 => "https://mirrors.huaweicloud.com/openjdk/17.0.10/openjdk-17.0.10_windows-x64_bin.zip",
                _ => throw new NotSupportedException($"Unsupported JDK version: {version}")
            };

            var zipPath = Path.Combine(jdkRoot, $"jdk-{version}.zip");
            using (var client = new WebClient())
            {
                client.DownloadProgressChanged += (s, e) =>
                {
                    // e.ProgressPercentage is 0–100, so we just pass it straight through
                    progress.Report(($"Downloading JDK {version}... {e.ProgressPercentage}%", e.ProgressPercentage));
                };

                await client.DownloadFileTaskAsync(jdkUrl, zipPath);
            }

            progress.Report(($"Download complete, extracting JDK {version}...", 100));


            // Extract JDK
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, jdkRoot);
            File.Delete(zipPath); // Clean up zip file

            // Find the extracted JDK directory
            var jdkDir = Directory.GetDirectories(jdkRoot)
                .FirstOrDefault(d => d.Contains($"jdk-{version}"));

            if (jdkDir == null)
                throw new Exception("Failed to find JDK in downloaded package");

            progress.Report(($"JDK {version} installed successfully!", 97));
            return jdkDir;
        }
        catch (Exception ex)
        {
            await ShowMessageAsync($"JDK installation failed: {ex.Message}", "Error");
            return null;
        }
    }

    private async Task ShowMessageAsync(string message, string title)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    public async Task<string> EnsureRequiredJdkAsync(string minecraftVersion,
                                                    IProgress<(string Message, int Progress)> progress)
    {
        int requiredJava = GetRequiredJavaVersion(minecraftVersion);
        var jdkHome = FindBestJdkHome(requiredJava);

        if (jdkHome == null)
        {
            // Prompt user to download JDK
            bool download = await ShowDownloadDialogAsync(requiredJava);
            if (!download)
                throw new OperationCanceledException($"Java {requiredJava} is required but not installed");

            jdkHome = await DownloadAndInstallJdkAsync(requiredJava, progress);
            if (jdkHome == null)
                throw new Exception($"Failed to install JDK {requiredJava}");
        }
        else
        {
            Debug.WriteLine($"[JDK] Found suitable JDK at {jdkHome}");
        }

        return jdkHome;
    }
}
