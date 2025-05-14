using System.Net.NetworkInformation;

namespace Modrix.Services
{
    public interface IConnectivityService
    {
        bool IsInternetAvailable();
    }

    public class ConnectivityService : IConnectivityService
    {
        public bool IsInternetAvailable()
        {
            try
            {
                using var ping = new Ping();
                var result = ping.Send("8.8.8.8", 2000); // Google's DNS, 2 second timeout
                return result?.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }
    }
}