using CommunityToolkit.Mvvm.ComponentModel;

namespace ModrixInstaller.ViewModels.Pages
{
    public partial class WelcomePageViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _welcomeTitle = "Welcome to Modrix";

        [ObservableProperty]
        private string _welcomeMessage = 
            "Modrix is a powerful Minecraft mod development IDE that simplifies the creation of mods for Fabric and Forge.\n\n" +
            "This setup wizard will guide you through the installation of Modrix on your computer.\n\n" +
            "Features:\n" +
            "• Visual mod element editor\n" +
            "• Built-in code templates for Fabric and Forge\n" +
            "• Integrated build and testing tools\n" +
            "• Resource management system\n" +
            "• Modern WPF-based interface\n\n" +
            "Click Next to continue with the installation.";

        [ObservableProperty]
        private string _systemRequirements = 
            "System Requirements:\n" +
            "• Windows 10 or later\n" +
            "• .NET 9.0 Runtime\n" +
            "• 150 MB available disk space\n" +
            "• Java 17 or later (for mod development)\n" +
            "• Internet connection (for updates)";

        [ObservableProperty]
        private string _versionInfo = "Version 1.0.0";

        [ObservableProperty]
        private string _copyrightInfo = "© 2024 Modrix Development Team";
    }
}