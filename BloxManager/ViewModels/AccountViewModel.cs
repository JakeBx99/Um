using BloxManager.Models;
using BloxManager.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace BloxManager.ViewModels
{
    public partial class AccountViewModel : ObservableObject
    {
        private readonly ILogger<AccountViewModel> _logger;
        private readonly IAccountService _accountService;
        private readonly IBrowserService _browserService;
        private readonly IGameService _gameService;

        [ObservableProperty]
        private Account _account = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isEditing;

        public AccountViewModel(
            ILogger<AccountViewModel> logger,
            IAccountService accountService,
            IBrowserService browserService,
            IGameService gameService)
        {
            _logger = logger;
            _accountService = accountService;
            _browserService = browserService;
            _gameService = gameService;
        }

        public async Task LoadAccountAsync(string accountId)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading account...";

                var account = await _accountService.GetAccountAsync(accountId);
                if (account != null)
                {
                    Account = account;
                    StatusMessage = "Account loaded";
                }
                else
                {
                    StatusMessage = "Account not found";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load account {accountId}");
                StatusMessage = "Error loading account";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task SaveAccountAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Saving account...";

                await _accountService.UpdateAccountAsync(Account);
                IsEditing = false;
                StatusMessage = "Account saved";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to save account {Account.Username}");
                StatusMessage = "Error saving account";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task LaunchBrowserAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Launching browser...";

                var success = await _browserService.LaunchBrowserAsync(Account);
                StatusMessage = success ? "Browser launched" : "Failed to launch browser";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to launch browser for {Account.Username}");
                StatusMessage = "Error launching browser";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task CloseBrowserAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Closing browser...";

                var success = await _browserService.CloseBrowserAsync(Account);
                StatusMessage = success ? "Browser closed" : "Failed to close browser";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to close browser for {Account.Username}");
                StatusMessage = "Error closing browser";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RefreshAccountAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Refreshing account...";

                await _accountService.RefreshAccountAsync(Account);
                StatusMessage = "Account refreshed";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to refresh account {Account.Username}");
                StatusMessage = "Error refreshing account";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void StartEditing()
        {
            IsEditing = true;
        }

        [RelayCommand]
        private void CancelEditing()
        {
            IsEditing = false;
            // TODO: Reload account to discard changes
        }
    }
}
