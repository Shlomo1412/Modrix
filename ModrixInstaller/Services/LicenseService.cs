namespace ModrixInstaller.Services
{
    public class LicenseService
    {
        public string GetLicenseText()
        {
            return """
                MIT License

                Copyright (c) 2024 Modrix Development Team

                Permission is hereby granted, free of charge, to any person obtaining a copy
                of this software and associated documentation files (the "Software"), to deal
                in the Software without restriction, including without limitation the rights
                to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
                copies of the Software, and to permit persons to whom the Software is
                furnished to do so, subject to the following conditions:

                The above copyright notice and this permission notice shall be included in all
                copies or substantial portions of the Software.

                THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
                IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
                FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
                AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
                LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
                OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
                SOFTWARE.

                Third-party components:

                1. WPF-UI (MIT License)
                   Copyright (c) 2022 Leszek Pomianowski and WPF UI Contributors

                2. CommunityToolkit.Mvvm (MIT License)
                   Copyright (c) .NET Foundation and Contributors

                3. Microsoft.Extensions.Hosting (MIT License)
                   Copyright (c) .NET Foundation and Contributors

                Additional Terms:

                This software is designed for educational and development purposes.
                The authors are not responsible for any mods created using this software.
                Users are responsible for ensuring their mods comply with Minecraft's EULA
                and any applicable platform guidelines.

                Support:
                For issues, feature requests, or contributions, please visit:
                https://github.com/Shlomo1412/Modrix

                Documentation:
                Full documentation is available at the project repository.
                """;
        }

        public bool IsLicenseAccepted { get; set; } = false;

        public void AcceptLicense()
        {
            IsLicenseAccepted = true;
        }

        public void RejectLicense()
        {
            IsLicenseAccepted = false;
        }
    }
}