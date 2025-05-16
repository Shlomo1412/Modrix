using Scriban;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Modrix.Views.Windows;
using Modrix.Models;

namespace Modrix.Services
{
    public class TemplateManager
    {
        private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
        private const string ResourceBase = "Modrix.Resources.Templates.Forge.";
        private const string ForgeVersion = "54.1.3";
        private const string WrapperRelPath = "Resources/Templates/Forge/Wrapper";

        
        public static readonly string ProjectsBasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Modrix",
            "Projects"
        );

        public TemplateManager()
        {
            
            Directory.CreateDirectory(ProjectsBasePath);
        }

        /// <summary>
        /// Recursively copy a directory (files + subdirectories).
        /// </summary>
        private void DirectoryCopy(string sourceDir, string destDir)
        {
            var dirInfo = new DirectoryInfo(sourceDir);
            if (!dirInfo.Exists) throw new DirectoryNotFoundException(sourceDir);

            Directory.CreateDirectory(destDir);
            foreach (var file in dirInfo.GetFiles())
                file.CopyTo(Path.Combine(destDir, file.Name), overwrite: true);
            foreach (var sub in dirInfo.GetDirectories())
                DirectoryCopy(sub.FullName, Path.Combine(destDir, sub.Name));
        }

        /// <summary>
        /// Copy the entire Gradle wrapper folder into the new project directory.
        /// </summary>
        private void CopyWrapperToProject(string projectDir)
        {
            string exeDir = Path.GetDirectoryName(_assembly.Location)!;
            string wrapperSrc = Path.Combine(exeDir, WrapperRelPath);
            string wrapperDst = projectDir;

            DirectoryCopy(wrapperSrc, wrapperDst);
        }

        /// <summary>
        /// Load an embedded *.scriban template.
        /// </summary>
        private string LoadTemplateText(string fileName)
        {
            string resource = ResourceBase + fileName;
            using var stream = _assembly.GetManifestResourceStream(resource)
                ?? throw new FileNotFoundException($"Resource '{resource}' not found.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private async Task RenderTemplateAsync(string templateName, string outputPath, object model)
        {
            var text = LoadTemplateText(templateName);
            var tpl = Template.Parse(text);
            if (tpl.HasErrors)
                throw new InvalidOperationException($"Errors in {templateName}: {string.Join("; ", tpl.Messages)}");

            var result = tpl.Render(model, member => member.Name);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var utf8NoBom = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            await File.WriteAllTextAsync(outputPath, result, utf8NoBom);
        }

        /// <summary>
        /// High-level: copy wrapper, generate files, then run gradle.
        /// </summary>
        public async Task FullSetupWithGradle(ModProjectData data,
                                              IProgress<(string Message, int Progress)> progress,
                                              string gradleArgs = "genIntellijRuns")
        {
            
            data.Location = Path.Combine(ProjectsBasePath, data.ModId);
            data.ProjectDir = data.Location;
            data.SrcDir = Path.Combine(data.ProjectDir, "src", "main");
            data.JavaDir = Path.Combine(data.SrcDir, "java");
            data.ResourcesDir = Path.Combine(data.SrcDir, "resources");
            data.PackageDir = Path.Combine(data.JavaDir, data.Package.Replace('.', Path.DirectorySeparatorChar));

            // 0. Copy wrapper into project
            progress.Report(("Copying Gradle wrapper...", 5));
            CopyWrapperToProject(data.ProjectDir);

            // 1. Generate all templates
            await SetupModProject(data, progress);

            // 2. (Optional) chmod on Unix
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var chmod = new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"+x {Path.Combine(data.ProjectDir, "gradlew")}",
                    WorkingDirectory = data.ProjectDir,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(chmod)?.WaitForExit();
            }

            // 3. Run gradlew.bat from within projectDir
            progress.Report(("Running Gradle wrapper...", 95));
            await RunGradleAsync(data.ProjectDir, gradleArgs, progress);

            progress.Report(("Done!", 100));
        }

        /// <summary>
        /// Run the project's gradlew(.bat) with given args.
        /// </summary>
        private async Task RunGradleAsync(string projectDir,
                                          string args,
                                          IProgress<(string Message, int Progress)> progress)
        {
            string exe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(projectDir, "gradlew.bat")
                : Path.Combine(projectDir, "gradlew");

            if (!File.Exists(exe))
                throw new FileNotFoundException($"Gradle wrapper not found at '{exe}'");

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = projectDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var tcs = new TaskCompletionSource<bool>();

            proc.OutputDataReceived += (s, e) => { if (e.Data != null) progress.Report((e.Data, -1)); };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) progress.Report((e.Data, -1)); };
            proc.Exited += (s, e) =>
            {
                if (proc.ExitCode == 0) tcs.SetResult(true);
                else tcs.SetException(new Exception($"Gradle exited with code {proc.ExitCode}"));
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await tcs.Task;
        }

        /// <summary>
        /// Generate the four templated files plus icon.
        /// </summary>
        public async Task SetupModProject(ModProjectData data,
                                          IProgress<(string Message, int Progress)> progress)
        {
            // folders
            progress.Report(("Creating folders...", 10));
            Directory.CreateDirectory(data.JavaDir);
            Directory.CreateDirectory(data.ResourcesDir);
            Directory.CreateDirectory(Path.Combine(data.ResourcesDir, "META-INF"));

            // build.gradle
            progress.Report(("Writing build.gradle...", 30));
            await RenderTemplateAsync("build.gradle.scriban",
                Path.Combine(data.ProjectDir, "build.gradle"),
                new
                {
                    package = data.Package,
                    minecraft_version = data.MinecraftVersion,
                    forge_version = ForgeVersion
                });

            // settings.gradle
            progress.Report(("Writing settings.gradle...", 40));
            await RenderTemplateAsync("settings.gradle.scriban",
                Path.Combine(data.ProjectDir, "settings.gradle"),
                new { modid = data.ModId });

            // gradle.properties
            progress.Report(("Writing gradle.properties...", 50));
            await RenderTemplateAsync(
                "gradle.properties.scriban",
                Path.Combine(data.ProjectDir, "gradle.properties"),
                new
                {
                    minecraft_version = data.MinecraftVersion,
                    forge_version = ForgeVersion,
                    forge_version_major = ForgeVersion.Split('.')[0],
                    mapping_channel = "official",
                    modid = data.ModId,
                    name = data.Name,
                    description = data.Description,
                    authors = data.Authors,
                    mod_license = data.License,
                    mod_version = data.ModVersion,
                    package = data.Package
                }
            );

            // mods.toml
            progress.Report(("Writing mods.toml...", 60));
            await RenderTemplateAsync("mods.toml.scriban",
                Path.Combine(data.ResourcesDir, "META-INF", "mods.toml"),
                new
                {
                    modid = data.ModId,
                    minecraft_version = data.MinecraftVersion,
                    forge_version = ForgeVersion,
                    name = data.Name
                });

            progress.Report(("Writing .mixins.json...", 65));
            await RenderTemplateAsync("mixins.json.scriban",
                Path.Combine(data.ResourcesDir, ".mixins.json"),
                new { modid = data.ModId });

            // MainMod.java
            progress.Report(("Writing MainMod.java...", 80));
            await RenderTemplateAsync("MainMod.java.scriban",
                Path.Combine(data.PackageDir, $"{CapFirst(data.ModId)}Mod.java"),
                new
                {
                    package = data.Package,
                    modid = data.ModId,
                    name = data.Name
                });

            // icon
            if (!string.IsNullOrEmpty(data.IconPath))
            {
                progress.Report(("Copying icon.png...", 90));
                File.Copy(data.IconPath,
                          Path.Combine(data.ResourcesDir, "icon.png"),
                          overwrite: true);
            }
        }

        
        public static List<ModProjectData> LoadAllProjects()
        {
            var projects = new List<ModProjectData>();
            
            if (!Directory.Exists(ProjectsBasePath))
                return projects;

            foreach (var projectDir in Directory.GetDirectories(ProjectsBasePath))
            {
                var modToml = Path.Combine(projectDir, "src", "main", "resources", "META-INF", "mods.toml");
                var gradleProperties = Path.Combine(projectDir, "gradle.properties");
                
                if (File.Exists(modToml) && File.Exists(gradleProperties))
                {
                    var project = ParseProjectData(projectDir, modToml, gradleProperties);
                    if (project != null)
                        projects.Add(project);
                }
            }

            return projects;
        }

        private static ModProjectData ParseProjectData(string projectDir, string modToml, string gradleProperties)
        {
            try
            {
                var modId = Path.GetFileName(projectDir);
                var iconPath = Path.Combine(projectDir, "src", "main", "resources", "icon.png");

                
                var gradleProps = File.ReadAllLines(gradleProperties)
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                    .ToDictionary(
                        line => line.Split('=')[0].Trim(),
                        line => line.Split('=')[1].Trim()
                    );

                return new ModProjectData
                {
                    Name = gradleProps.GetValueOrDefault("mod_name", modId),
                    ModId = modId,
                    Package = gradleProps.GetValueOrDefault("package", ""),
                    Location = projectDir,
                    IconPath = File.Exists(iconPath) ? iconPath : null,
                    ModType = "Forge Mod",
                    MinecraftVersion = gradleProps.GetValueOrDefault("minecraft_version", ""),
                    Description = gradleProps.GetValueOrDefault("mod_description", ""),
                    Authors = gradleProps.GetValueOrDefault("mod_authors", ""),
                    License = gradleProps.GetValueOrDefault("mod_license", ""),
                    ModVersion = gradleProps.GetValueOrDefault("mod_version", "1.0.0")
                };
            }
            catch
            {
                return null;
            }
        }

        private static string CapFirst(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
