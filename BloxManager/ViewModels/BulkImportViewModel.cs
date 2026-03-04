using BloxManager.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BloxManager.ViewModels
{
    public partial class BulkImportViewModel : ObservableObject
    {
        private readonly ILogger<BulkImportViewModel>? _logger;
        private readonly IAccountService? _accountService;

        [ObservableProperty]
        private string _inputText = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _importType = "UserPass"; // UserPass or Cookie

        public BulkImportViewModel()
        {
            // This constructor is for design-time support
        }

        public BulkImportViewModel(ILogger<BulkImportViewModel> logger, IAccountService accountService)
        {
            _logger = logger;
            _accountService = accountService;
        }

        public void SetImportType(string type)
        {
            ImportType = type;
            
            // Update UI text based on import type
            if (type == "UserPass")
            {
                StatusMessage = "Separate the accounts with new lines";
                // FormatText = "Format: user:pass";
            }
            else if (type == "Cookie")
            {
                StatusMessage = "Separate the cookies with new lines";
                // FormatText = "Format: ROBLOSECURITY cookie";
            }
        }

        [RelayCommand]
        private async Task ImportAsync()
        {
            if (string.IsNullOrWhiteSpace(InputText))
            {
                StatusMessage = "No input provided";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Importing accounts...";

                var lines = InputText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var importTasks = new List<Task<(bool Success, string Message, string Account)>>();

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine)) continue;

                    if (ImportType == "UserPass")
                    {
                        if (!trimmedLine.Contains(':'))
                        {
                            _logger?.LogWarning("Invalid user:pass format: {Line}", trimmedLine);
                            continue;
                        }

                        var parts = trimmedLine.Split(':', 2);
                        if (parts.Length != 2)
                        {
                            _logger?.LogWarning("Invalid user:pass format: {Line}", trimmedLine);
                            continue;
                        }

                        var username = parts[0].Trim();
                        var password = parts[1].Trim();

                        importTasks.Add(ImportUserPassAsync(username, password));
                    }
                    else if (ImportType == "Cookie")
                    {
                        importTasks.Add(ImportCookieAsync(trimmedLine));
                    }
                }

                // Process imports in parallel batches to avoid overwhelming the system
                const int batchSize = 5;
                var results = new List<(bool Success, string Message, string Account)>();

                for (int i = 0; i < importTasks.Count; i += batchSize)
                {
                    var batch = importTasks.Skip(i).Take(batchSize);
                    var batchResults = await Task.WhenAll(batch);
                    results.AddRange(batchResults);

                    // Update progress
                    var processedCount = Math.Min(i + batchSize, importTasks.Count);
                    StatusMessage = $"Importing accounts... {processedCount}/{importTasks.Count}";
                }

                // Summary
                var successCount = results.Count(r => r.Success);
                var failureCount = results.Count(r => !r.Success);

                StatusMessage = $"Import complete: {successCount} successful, {failureCount} failed";

                // Log failures for debugging
                foreach (var (success, message, account) in results.Where(r => !r.Success))
                {
                    _logger?.LogWarning("Import failed for {Account}: {Message}", account, message);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during bulk import");
                StatusMessage = $"Import failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task<(bool Success, string Message, string Account)> ImportUserPassAsync(string username, string password)
        {
            try
            {
                if (_accountService == null)
                    return (false, "Account service unavailable", username);

                var account = await _accountService.LoginAsync(username, password, keepBrowserOpenUntilSuccess: true);
                if (account != null)
                {
                    _logger?.LogInformation("Successfully imported user:pass account: {Username}", username);
                    return (true, "Success", username);
                }
                else
                {
                    return (false, "Login failed", username);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to import user:pass account: {Username}", username);
                return (false, ex.Message, username);
            }
        }

        private async Task<(bool Success, string Message, string Account)> ImportCookieAsync(string cookie)
        {
            try
            {
                if (_accountService == null)
                    return (false, "Account service unavailable", "Cookie");

                var account = await _accountService.LoginWithCookieAsync(cookie);
                if (account != null)
                {
                    _logger?.LogInformation("Successfully imported cookie account: {Username}", account.Username);
                    return (true, "Success", account.Username);
                }
                else
                {
                    return (false, "Invalid cookie", "Cookie");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to import cookie account");
                return (false, ex.Message, "Cookie Account");
            }
        }
    }
}
