using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ModrixInstaller.Services
{
    public class LicenseService : INotifyPropertyChanged
    {
        private bool _isLicenseAccepted;
        public bool IsLicenseAccepted
        {
            get => _isLicenseAccepted;
            set
            {
                if (SetProperty(ref _isLicenseAccepted, value))
                {
                    OnPropertyChanged();
                }
            }
        }

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

               Additional Terms:

               This installer and the Modrix software are provided for educational and
               development purposes. By using this software, you agree to:

               1. Use the software in compliance with Mojang's Minecraft EULA
               2. Not use the software for commercial purposes without proper licensing
               3. Respect intellectual property rights of Minecraft and its assets
               4. Follow community guidelines and best practices for mod development

               For more information about licensing and usage rights, please visit:
               https://github.com/Shlomo1412/Modrix/wiki/License
               """;
        }

        public void AcceptLicense()
        {
            IsLicenseAccepted = true;
        }

        public void DeclineLicense()
        {
            IsLicenseAccepted = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}