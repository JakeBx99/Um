using BloxManager.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

namespace BloxManager.Services
{
    public class GameService : IGameService
    {
        private readonly ILogger<GameService> _logger;
        private readonly IAccountService _accountService;
        private readonly IRobloxService _robloxService;
        private readonly ISettingsService _settingsService;
        private List<Game> _favoriteGames = new();
        private List<Game> _recentGames = new();
        private readonly string _dataFilePath;

        public GameService(
            ILogger<GameService> logger,
            IAccountService accountService,
            IRobloxService robloxService,
            ISettingsService settingsService)
        {
            _logger = logger;
            _accountService = accountService;
            _robloxService = robloxService;
            _settingsService = settingsService;
            
            var appDataPath = AppContext.BaseDirectory;
            
            Directory.CreateDirectory(appDataPath);
            _dataFilePath = Path.Combine(appDataPath, "games.json");
            
            LoadGameData();
        }

        public async Task<List<Game>> GetPopularGamesAsync(int limit = 50)
        {
            try
            {
                // This would typically call Roblox API to get popular games
                // For now, return empty list as this requires API implementation
                var games = new List<Game>();
                
                // TODO: Implement Roblox API call for popular games
                // GET /v1/games/popular?limit={limit}
                
                return games.Take(limit).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get popular games");
                return new List<Game>();
            }
        }

        public async Task<List<Game>> GetFavoriteGamesAsync()
        {
            return _favoriteGames.ToList();
        }

        public async Task AddFavoriteGameAsync(Game game)
        {
            if (!_favoriteGames.Any(g => g.Id == game.Id))
            {
                _favoriteGames.Add(game);
                await SaveGameDataAsync();
                _logger.LogInformation($"Added game {game.Name} to favorites");
            }
        }

        public async Task RemoveFavoriteGameAsync(long gameId)
        {
            var game = _favoriteGames.FirstOrDefault(g => g.Id == gameId);
            if (game != null)
            {
                _favoriteGames.Remove(game);
                await SaveGameDataAsync();
                _logger.LogInformation($"Removed game {game.Name} from favorites");
            }
        }

        public async Task<List<Game>> GetRecentGamesAsync()
        {
            return _recentGames.OrderByDescending(g => g.Updated).Take(20).ToList();
        }

        public async Task AddRecentGameAsync(Game game)
        {
            var existingGame = _recentGames.FirstOrDefault(g => g.Id == game.Id);
            if (existingGame != null)
            {
                _recentGames.Remove(existingGame);
            }
            else if (_recentGames.Count >= 20)
            {
                _recentGames.RemoveAt(0);
            }

            game.Updated = DateTime.Now;
            _recentGames.Add(game);
            await SaveGameDataAsync();
        }

        public async Task<Game?> GetGameDetailsAsync(long placeId)
        {
            try
            {
                return await _robloxService.GetGameInfoAsync(placeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get game details for place {placeId}");
                return null;
            }
        }

        public async Task<List<Server>> GetGameServersAsync(long placeId, int limit = 100)
        {
            try
            {
                return await _robloxService.GetServersAsync(placeId, limit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get servers for place {placeId}");
                return new List<Server>();
            }
        }

        public async Task<bool> JoinGameAsync(Account account, long placeId, string? jobId = null, string? launchData = null)
        {
            try
            {
                var success = await _robloxService.JoinGameAsync(account, placeId, jobId, launchData);
                if (success)
                {
                    var game = await GetGameDetailsAsync(placeId);
                    if (game != null)
                    {
                        await AddRecentGameAsync(game);
                    }
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to join game {placeId} with account {account.Username}");
                return false;
            }
        }

        public async Task<bool> JoinServerAsync(Account account, long placeId, string serverId)
        {
            try
            {
                var success = await _robloxService.JoinServerAsync(account, placeId, serverId);
                if (success)
                {
                    var game = await GetGameDetailsAsync(placeId);
                    if (game != null)
                    {
                        await AddRecentGameAsync(game);
                    }
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to join server {serverId} in game {placeId} with account {account.Username}");
                return false;
            }
        }

        public async Task<Account?> GetAccountById(string accountId)
        {
            var accounts = await _accountService.GetAccountsAsync();
            return accounts.FirstOrDefault(a => a.Id == accountId);
        }

        public async Task<bool> IsGameRunningAsync(Account account)
        {
            try
            {
                if (string.IsNullOrEmpty(account.BrowserTrackerId)) return false;

                // Use WMI to find RobloxPlayerBeta.exe processes and check their command line for the BrowserTrackerId
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'RobloxPlayerBeta.exe'");
                
                using var objects = searcher.Get();
                var processes = Process.GetProcessesByName("RobloxPlayerBeta");

                foreach (ManagementObject obj in objects)
                {
                    string commandLine = obj["CommandLine"]?.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(commandLine)) continue;
                    
                    var pidObj = obj["ProcessId"];
                    if (pidObj == null) continue;
                    int pid = Convert.ToInt32(pidObj);

                    bool hasLauncher = commandLine.IndexOf("placelauncherurl", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!hasLauncher) continue;

                    var id = System.Text.RegularExpressions.Regex.Escape(account.BrowserTrackerId);
                    var patterns = new[]
                    {
                        $@"browsertrackerid:\s*{id}",
                        $@"browserTrackerId=\s*{id}"
                    };
                    foreach (var pattern in patterns)
                    {
                        if (System.Text.RegularExpressions.Regex.IsMatch(commandLine, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            var p = processes.FirstOrDefault(x => x.Id == pid);
                            if (p != null)
                            {
                                try
                                {
                                    // If process has no main window handle and has been running for > 30s, it's a hung ghost process.
                                    if (p.MainWindowHandle == IntPtr.Zero && (DateTime.Now - p.StartTime).TotalSeconds > 30)
                                    {
                                        continue;
                                    }
                                }
                                catch { } // Ignore access denied on StartTime
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to check if game is running for account {account.Username}");
                return false;
            }
        }

        public async Task<bool> StopGameAsync(Account account)
        {
            try
            {
                var processes = Process.GetProcessesByName("RobloxPlayerBeta");
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit();
                    }
                    catch
                    {
                        // Ignore if process is already terminating
                    }
                }

                _logger.LogInformation($"Stopped Roblox processes for account {account.Username}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to stop game for account {account.Username}");
                return false;
            }
        }

        private void LoadGameData()
        {
            try
            {
                if (!File.Exists(_dataFilePath))
                {
                    _favoriteGames = new List<Game>();
                    _recentGames = new List<Game>();
                    var defaultData = new
                    {
                        FavoriteGames = _favoriteGames,
                        RecentGames = _recentGames
                    };
                    var defaultJson = JsonConvert.SerializeObject(defaultData, Formatting.Indented);
                    File.WriteAllText(_dataFilePath, defaultJson);
                    return;
                }

                var json = File.ReadAllText(_dataFilePath);
                var data = JsonConvert.DeserializeAnonymousType(json, new
                {
                    FavoriteGames = new List<Game>(),
                    RecentGames = new List<Game>()
                });

                if (data != null)
                {
                    _favoriteGames = data.FavoriteGames ?? new List<Game>();
                    _recentGames = data.RecentGames ?? new List<Game>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load game data");
            }
        }

        private async Task SaveGameDataAsync()
        {
            try
            {
                var data = new
                {
                    FavoriteGames = _favoriteGames,
                    RecentGames = _recentGames
                };

                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                await File.WriteAllTextAsync(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save game data");
            }
        }
    }
}
