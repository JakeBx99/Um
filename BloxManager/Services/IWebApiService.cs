using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace BloxManager.Services
{
    public interface IWebApiService
    {
        Task StartAsync();
        Task StopAsync();
        bool IsRunning { get; }
    }
}
