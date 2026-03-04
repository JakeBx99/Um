using BloxManager.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BloxManager.Services
{
    public class WebApiService : IWebApiService
    {
        private readonly ILogger<WebApiService> _logger;
        private readonly IAccountService _accountService;
        private readonly IGameService _gameService;
        private readonly ISettingsService _settingsService;
        private bool _isRunning;

        public bool IsRunning => _isRunning;

        public WebApiService(
            ILogger<WebApiService> logger,
            IAccountService accountService,
            IGameService gameService,
            ISettingsService settingsService)
            {
            _logger = logger;
            _accountService = accountService;
            _gameService = gameService;
            _settingsService = settingsService;
        }

        public Task StartAsync()
        {
            try
            {
                _isRunning = true;
                _logger.LogInformation("Web API service started (simplified version)");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Web API");
                throw;
            }
        }

        public Task StopAsync()
        {
            try
            {
                _isRunning = false;
                _logger.LogInformation("Web API service stopped");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop Web API");
                throw;
            }
        }

        public async Task<string> GetAccountsAsync()
        {
            try
            {
                var accounts = await _accountService.GetAccountsAsync();
                var result = accounts.Select(a => new
                {
                    a.Id,
                    a.Username,
                    a.Alias,
                    a.Group,
                    a.IsValid,
                    a.LastUsed,
                    a.UserId,
                    a.IsFavorite
                });
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get accounts");
                return JsonConvert.SerializeObject(new { error = ex.Message });
            }
        }

        public async Task<string> GetAccountAsync(string id)
        {
            try
            {
                var account = await _accountService.GetAccountAsync(id);
                if (account == null)
                {
                    return JsonConvert.SerializeObject(new { error = "Account not found" });
                }

                var result = new
                {
                    account.Id,
                    account.Username,
                    account.Alias,
                    account.Group,
                    account.IsValid,
                    account.LastUsed,
                    account.UserId,
                    account.IsFavorite
                };
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get account {id}");
                return JsonConvert.SerializeObject(new { error = ex.Message });
            }
        }

        public async Task<string> LaunchAccountAsync(string id, string? placeId = null, string? jobId = null)
        {
            try
            {
                var account = await _accountService.GetAccountAsync(id);
                if (account == null)
                {
                    return JsonConvert.SerializeObject(new { error = "Account not found" });
                }

                if (long.TryParse(placeId, out var placeIdNum))
                {
                    var success = await _gameService.JoinGameAsync(account, placeIdNum, jobId);
                    return JsonConvert.SerializeObject(new { success, message = success ? "Game launched successfully" : "Failed to launch game" });
                }
                else
                {
                    return JsonConvert.SerializeObject(new { error = "Invalid Place ID" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to launch account {id}");
                return JsonConvert.SerializeObject(new { error = ex.Message });
            }
        }

        public async Task<string> RefreshAccountAsync(string id)
        {
            try
            {
                var account = await _accountService.GetAccountAsync(id);
                if (account == null)
                {
                    return JsonConvert.SerializeObject(new { error = "Account not found" });
                }

                await _accountService.RefreshAccountAsync(account);
                return JsonConvert.SerializeObject(new { success = true, message = "Account refreshed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to refresh account {id}");
                return JsonConvert.SerializeObject(new { error = ex.Message });
            }
        }

        public async Task<string> GetServersAsync(long placeId, int limit = 100)
        {
            try
            {
                var servers = await _gameService.GetGameServersAsync(placeId, limit);
                var result = servers.Select(s => new
                {
                    s.Id,
                    s.MaxPlayers,
                    s.Playing,
                    s.Ping,
                    s.Fps,
                    s.Region,
                    s.RegionCode,
                    s.Capacity
                });
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get servers for place {placeId}");
                return JsonConvert.SerializeObject(new { error = ex.Message });
            }
        }

        public async Task<string> JoinGameAsync(string accountId, long placeId, string? jobId = null)
        {
            try
            {
                var account = await GetAccountById(accountId);
                if (account == null)
                {
                    return JsonConvert.SerializeObject(new { error = "Account not found" });
                }

                var success = await _gameService.JoinGameAsync(account, placeId, jobId);
                return JsonConvert.SerializeObject(new { success, message = success ? "Game joined successfully" : "Failed to join game" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to join game");
                return JsonConvert.SerializeObject(new { error = ex.Message });
            }
        }

        public async Task<string> GetPopularGamesAsync(int limit = 50)
        {
            try
            {
                var games = await _gameService.GetPopularGamesAsync(limit);
                var result = games.Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.Description,
                    Creator = new { g.Creator.Name, g.Creator.Id },
                    OnlineCount = 0 // Game model doesn't have Playing property
                });
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get popular games");
                return JsonConvert.SerializeObject(new { error = ex.Message });
            }
        }

        public async Task<string> GetFavoriteGamesAsync()
        {
            try
            {
                var games = await _gameService.GetFavoriteGamesAsync();
                var result = games.Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.Description,
                    Creator = new { g.Creator.Name, g.Creator.Id },
                    OnlineCount = 0 // Game model doesn't have Playing property
                });
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get favorite games");
                return JsonConvert.SerializeObject(new { error = ex.Message });
            }
        }

        private async Task<Account?> GetAccountById(string accountId)
        {
            var accounts = await _accountService.GetAccountsAsync();
            return accounts.FirstOrDefault(a => a.Id == accountId);
        }
    }
}
