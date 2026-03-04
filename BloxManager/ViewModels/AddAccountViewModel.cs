using BloxManager.Models;
using BloxManager.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BloxManager.ViewModels
{
    public partial class AddAccountViewModel : ObservableObject
    {
        private readonly ILogger<AddAccountViewModel> _logger;
        private readonly IAccountService _accountService;
        private readonly IBrowserService _browserService;

        private CancellationTokenSource? _cts;

        public event Action<bool?>? RequestClose;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public Account? AddedAccount { get; private set; }

        public AddAccountViewModel(
            ILogger<AddAccountViewModel> logger,
            IAccountService accountService,
            IBrowserService browserService)
        {
            _logger = logger;
            _accountService = accountService;
            _browserService = browserService;
        }

        [RelayCommand]
        private async Task AddAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            StatusMessage = "Opening Roblox login...";
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            try
            {
                var loginInfo = await _browserService.AcquireRobloxLoginInfoAsync(_cts.Token);
                if (loginInfo == null || string.IsNullOrWhiteSpace(loginInfo.SecurityToken))
                {
                    StatusMessage = "Login cancelled or no cookie detected.";
                    return;
                }
                StatusMessage = "Adding account...";
                var account = await _accountService.LoginWithCookieAsync(loginInfo.SecurityToken.Trim());
                if (account == null)
                {
                    StatusMessage = "Failed to add account. Invalid cookie?";
                    return;
                }

                // Apply captured password
                account.Password = loginInfo.Password;
                await _accountService.UpdateAccountAsync(account);

                AddedAccount = account;
                StatusMessage = $"Added {account.Username}";
                RequestClose?.Invoke(true);

            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Cancelled.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
            }
            RequestClose?.Invoke(false);
        }
    }
}
