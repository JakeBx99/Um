using BloxManager.Models;
using System.Threading;
using System.Threading.Tasks;

namespace BloxManager.Services
{
    public interface IBrowserService
    {
        Task<string?> LoginAndGetCookieAsync(string username, string password, System.Threading.CancellationToken cancellationToken = default);
        Task<string?> LoginAndGetCookieAsync(string username, string password, System.Threading.CancellationToken cancellationToken, bool closeOnlyOnSuccess);
        Task<bool> LaunchBrowserAsync(Account account);
        Task<bool> CloseBrowserAsync(Account account);
        Task<bool> IsBrowserRunningAsync(Account account);
        Task<string?> GetBrowserTrackerIdAsync(Account account);
        Task SetBrowserTrackerIdAsync(Account account, string trackerId);

        Task<RobloxLoginInfo?> AcquireRobloxLoginInfoAsync(CancellationToken cancellationToken = default);

        Task<bool> ApproveQuickLoginAsync(Account account, string code);

        Task<bool> JoinGameViaWebAsync(Account account, long placeId, string? jobId = null, string? launchData = null, CancellationToken cancellationToken = default);
    }
}
