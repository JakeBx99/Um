using BloxManager.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BloxManager.Services
{
    public interface IGameService
    {
        Task<List<Game>> GetPopularGamesAsync(int limit = 50);
        Task<List<Game>> GetFavoriteGamesAsync();
        Task AddFavoriteGameAsync(Game game);
        Task RemoveFavoriteGameAsync(long gameId);
        Task<List<Game>> GetRecentGamesAsync();
        Task AddRecentGameAsync(Game game);
        Task<Game?> GetGameDetailsAsync(long placeId);
        Task<List<Server>> GetGameServersAsync(long placeId, int limit = 100);
        Task<bool> JoinGameAsync(Account account, long placeId, string? jobId = null, string? launchData = null);
        Task<bool> JoinServerAsync(Account account, long placeId, string serverId);
        Task<bool> IsGameRunningAsync(Account account);
        Task<bool> StopGameAsync(Account account);
    }
}
