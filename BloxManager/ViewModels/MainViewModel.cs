using BloxManager.Models;
using BloxManager.Helpers;
using BloxManager.Services;
using BloxManager.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Data;
using System.ComponentModel;
using System.Windows;

namespace BloxManager.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ILogger<MainViewModel> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IAccountService _accountService;
        private readonly IGameService _gameService;
        private readonly ISettingsService _settingsService;
        private readonly IBrowserService _browserService;
        private readonly IRobloxService _robloxService;

        // ── Observable properties ────────────────────────────────────────────

        [ObservableProperty]
        private ObservableCollection<Account> _accounts = new();

        [ObservableProperty]
        private ObservableCollection<Account> _selectedAccounts = new();

        [ObservableProperty]
        private ObservableCollection<string> _groups = new();

        // FIX: Null reference warnings — initialise to non-null defaults
        [ObservableProperty]
        private Account? _selectedAccount = null;

        [ObservableProperty]
        private string _selectedGroup = "All";

        [ObservableProperty]
        private ObservableCollection<AccountGroup> _accountGroups = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _placeId = string.Empty;

        [ObservableProperty]
        private string _jobId = string.Empty;

        [ObservableProperty]
        private string _launchData = string.Empty;

        [ObservableProperty]
        private int _accountsInGame = 0;
        [ObservableProperty]
        private string _inGameUptimeText = "00:00:00";

        [RelayCommand]
        private void OpenSettings()
        {
            System.Diagnostics.Debug.WriteLine($"OpenSettings called - IsSettingsOpen was: {IsSettingsOpen}");
            IsSettingsOpen = true;
            System.Diagnostics.Debug.WriteLine($"OpenSettings completed - IsSettingsOpen now: {IsSettingsOpen}");
        }

        [RelayCommand]
        private void OpenAccounts()
        {
            System.Diagnostics.Debug.WriteLine($"OpenAccounts called - IsSettingsOpen was: {IsSettingsOpen}");
            IsSettingsOpen = false;
            System.Diagnostics.Debug.WriteLine($"OpenAccounts completed - IsSettingsOpen now: {IsSettingsOpen}");
        }

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _multiRobloxEnabled;

        [ObservableProperty]
        private bool _isSettingsOpen = false;

        partial void OnIsSettingsOpenChanged(bool value)
        {
            System.Diagnostics.Debug.WriteLine($"IsSettingsOpen property changed to: {value}");
        }

        [ObservableProperty]
        private SettingsViewModel _settingsViewModel;

        [ObservableProperty]
        private string _backgroundImagePath = string.Empty;

        [ObservableProperty]
        private string _backgroundImageStretch = "UniformToFill";

        [ObservableProperty]
        private string _backgroundImageAlignment = "Center";

        [ObservableProperty]
        private double _backgroundImageOpacity = 1.0;

        [ObservableProperty]
        private string _backgroundTargetDimensions = string.Empty;


        [ObservableProperty]
        private ObservableCollection<AccountListItem> _displayItems = new();


        public ObservableCollection<string> AvailableGroupsForMove { get; } = new();

        // ── Constructor ──────────────────────────────────────────────────────

        public MainViewModel(
            ILogger<MainViewModel> logger,
            ILoggerFactory loggerFactory,
            IAccountService accountService,
            IGameService gameService,
            ISettingsService settingsService,
            IBrowserService browserService,
            IRobloxService robloxService)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _accountService = accountService;
            _gameService = gameService;
            _settingsService = settingsService;
            _browserService = browserService;
            _robloxService = robloxService;

            _settingsViewModel = new SettingsViewModel(_loggerFactory.CreateLogger<SettingsViewModel>(), _settingsService, _gameService);
            _settingsViewModel.PropertyChanged += SettingsViewModel_PropertyChanged;

            Groups.CollectionChanged += (s, e) => UpdateAvailableGroupsForMove();


            Groups.CollectionChanged += (s, e) => UpdateAvailableGroupsForMove();
            
            AccountGroups.CollectionChanged += (s, e) => {
                _ = SavePersistentGroupsAsync();
            };

            _ = LoadDataAsync();
            _ = StartActiveAccountTrackingAsync();
            InitializeVersionChannel();
        }

        [ObservableProperty]
        private string _appVersion = string.Empty;

        [ObservableProperty]
        private string _updateChannel = "Made By Mr Duck";

        public string VersionChannelText => $"{AppVersion} {UpdateChannel}";

        private async void InitializeVersionChannel()
        {
            try
            {
                var fvi = FileVersionInfo.GetVersionInfo(Process.GetCurrentProcess().MainModule!.FileName);
                var ver = fvi.FileVersion ?? "0.1.0.0";
                // Show major.minor only by default
                var parts = ver.Split('.');
                var display = parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : ver;
                AppVersion = $"v{display}";
                UpdateChannel = "Made By Mr Duck";
            }
            catch
            {
                AppVersion = "v0.1";
                UpdateChannel = "Made By Mr Duck";
            }
        }

        private async Task StartActiveAccountTrackingAsync()
        {
            // Always run trackers; adjust frequency for low memory mode
            var lowMem = await _settingsService.GetLowMemoryModeAsync();
            var processIntervalMs = lowMem ? 30000 : 15000;
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await UpdateActiveAccountsCountAsync();
                    await Task.Delay(processIntervalMs); // interval for process checks
                }
            });
            _ = Task.Run(async () =>
            {
                UpdateInGameUptimeText(); // seed immediately
                while (true)
                {
                    UpdateInGameUptimeText();
                    await Task.Delay(1000); // Update timer every second
                }
            });
        }

        private async Task UpdateActiveAccountsCountAsync()
        {
            try
            {
                int activeCount = 0;
                var accounts = Accounts.ToList();
                
                foreach (var account in accounts)
                {
                    bool isLocalRunning = await _gameService.IsGameRunningAsync(account);
                    AccountStatus newStatus = AccountStatus.Offline;

                    if (isLocalRunning)
                    {
                        newStatus = AccountStatus.InGame;
                    }
                    else if (account.UserId > 0)
                    {
                        try
                        {
                            var presence = await _robloxService.GetUserPresenceAsync(account.UserId);
                            if (presence != null)
                            {
                                // UserPresenceType: 0 = Offline, 1 = Online, 2 = InGame, 3 = Studio
                                switch (presence.UserPresenceType)
                                {
                                    case 2:
                                        // If API says InGame but no local process, it's likely a ghost.
                                        // We'll set it to Offline to avoid confusing the user.
                                        newStatus = AccountStatus.Offline;
                                        break;
                                    case 1:
                                    case 3:
                                        // Online/Studio. Only show Blue if it was recently active to avoid 5-min ghosts.
                                        // For now, let's keep it blue but we could gate it by LastUsed.
                                        newStatus = AccountStatus.Online;
                                        break;
                                    default:
                                        newStatus = AccountStatus.Offline;
                                        break;
                                }
                            }
                        }
                        catch { }
                    }

                    if (newStatus == AccountStatus.InGame)
                    {
                        activeCount++;
                        if (account.InGameSince == null)
                            account.InGameSince = DateTime.Now;
                    }
                    else
                    {
                        account.InGameSince = null;
                    }

                    if (account.Status != newStatus)
                    {
                        account.Status = newStatus;
                    }
                }
                
                if (AccountsInGame != activeCount)
                {
                    AccountsInGame = activeCount;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update active accounts count");
            }
        }

        private void UpdateInGameUptimeText()
        {
            try
            {
                var since = Accounts.Where(a => a.InGameSince != null)
                                    .Select(a => a.InGameSince!.Value)
                                    .DefaultIfEmpty(DateTime.MinValue)
                                    .Min();
                if (since == DateTime.MinValue)
                {
                    if (InGameUptimeText != "00:00:00") InGameUptimeText = "00:00:00";
                    return;
                }
                var span = DateTime.Now - since;
                var text = span.TotalHours >= 1
                    ? $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}"
                    : $"{span.Minutes:D2}:{span.Seconds:D2}";
                if (InGameUptimeText != text) InGameUptimeText = text;
            }
            catch
            {
                // ignore timer errors
            }
        }

        // ── Data loading ─────────────────────────────────────────────────────

        private async Task LoadDataAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading accounts...";

            try
            {
                await LoadAccountsAsync();
                await LoadGroupsAsync();
                await LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load initial data");
                StatusMessage = "Error loading data";
            }
            finally
            {
                IsLoading = false;
                StatusMessage = "Ready";
            }
        }

        private async Task LoadAccountsAsync()
        {
            var accounts = await _accountService.GetAccountsAsync();
            Accounts.Clear();
            foreach (var account in accounts)
            {
                // Set default status for existing accounts without status
                if (account.Status == AccountStatus.Unknown)
                    account.Status = AccountStatus.Offline;
                
                // Load avatar URL if not already set
                if (string.IsNullOrEmpty(account.AvatarUrl) && account.UserId > 0)
                {
                    try
                    {
                        account.AvatarUrl = await _robloxService.GetUserAvatarUrlAsync(account.UserId) ?? string.Empty;
                    }
                    catch
                    {
                        // Avatar loading failed, continue without avatar
                        account.AvatarUrl = string.Empty;
                    }
                }
                
                Accounts.Add(account);
            }
        }

        private async Task LoadGroupsAsync()
        {
            var persistentGroups = await _settingsService.GetPersistentGroupsAsync();
            Groups.Clear();
            Groups.Add("All");
            Groups.Add("Default");

            
            foreach (var g in persistentGroups)
            {
                if (!Groups.Contains(g))
                    Groups.Add(g);
            }

            // Also add any groups found in accounts
            var accountGroups = await _accountService.GetGroupsAsync();
            foreach (var g in accountGroups)
            {
                if (!Groups.Contains(g))
                    Groups.Add(g);
            }

            UpdateAvailableGroupsForMove();
            UpdateDisplayItems();
        }

        public void UpdateDisplayItems()
        {
            var oldDisplayItems = DisplayItems.ToList();
            DisplayItems.Clear();
            
            var groupsToShow = SelectedGroup == "All" 
                ? Groups.Where(g => g != "All").ToList()
                : new List<string> { SelectedGroup };

            var accountsByGroup = Accounts.GroupBy(a => a.Group).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var groupName in groupsToShow)
            {
                // Try to find existing group object to preserve expansion state
                var groupItem = oldDisplayItems.OfType<AccountGroup>().FirstOrDefault(g => g.Name == groupName) 
                               ?? new AccountGroup(groupName);
                
                groupItem.Accounts.Clear();
                if (accountsByGroup.TryGetValue(groupName, out var accountsForGroup))
                {
                    foreach (var acc in accountsForGroup) groupItem.Accounts.Add(acc);
                }

                DisplayItems.Add(groupItem);

                System.Diagnostics.Debug.WriteLine($"Group: {groupItem.Name}, IsExpanded: {groupItem.IsExpanded}, Account count: {groupItem.Accounts.Count}");

                if (groupItem.IsExpanded)
                {
                    if (accountsByGroup.TryGetValue(groupName, out var accountsInGroup))
                    {
                        var filteredAccounts = accountsInGroup
                            .Where(a => string.IsNullOrEmpty(SearchText) || a.Username.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                            .OrderBy(a => a.SortOrder)
                            .ThenBy(a => a.Username);

                        foreach (var account in filteredAccounts)
                        {
                            DisplayItems.Add(account);
                            System.Diagnostics.Debug.WriteLine($"  Added account: {account.Username}");
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  Group {groupItem.Name} is collapsed - accounts hidden");
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Total display items: {DisplayItems.Count}");
        }


        [RelayCommand]
        private async Task MoveItemAsync(DragEventArgs e)
        {
            // This will be called from code-behind after drag-drop logic determines the move
        }

        public async Task ReorderItemsAsync(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || newIndex < 0 || oldIndex >= DisplayItems.Count || newIndex >= DisplayItems.Count) return;

            var items = DisplayItems.ToList();
            var itemToMove = items[oldIndex];
            items.RemoveAt(oldIndex);
            items.Insert(newIndex, itemToMove);

            // Re-calculate all sort orders based on the new list
            int order = 0;
            string currentGroup = "Default";
            int groupCount = 0;
            
            foreach (var item in items)
            {
                if (item is AccountGroup group)
                {
                    currentGroup = group.Name;
                    // Update the Groups collection order
                    int groupIdx = Groups.IndexOf(group.Name);
                    if (groupIdx != -1)
                    {
                        Groups.Move(groupIdx, Math.Min(groupCount++, Groups.Count - 1));
                    }
                }
                else if (item is Account account)
                {
                    account.SortOrder = order++;
                    account.Group = currentGroup;
                    await _accountService.UpdateAccountAsync(account);
                }
            }

            await SavePersistentGroupsAsync();
            UpdateDisplayItems();
        }


        private async Task SavePersistentGroupsAsync()
        {
            var currentGroups = Groups.Where(g => g != "All").ToList();
            await _settingsService.SetPersistentGroupsAsync(currentGroups);
        }


        private void UpdateAvailableGroupsForMove()
        {
            var moveGroups = Groups.Where(g => g != "All").ToList();
            
            // Sync AvailableGroupsForMove with the actual list
            // We do this instead of replacing the collection to keep bindings alive
            var toRemove = AvailableGroupsForMove.Where(g => !moveGroups.Contains(g)).ToList();
            foreach (var g in toRemove) AvailableGroupsForMove.Remove(g);
            
            foreach (var g in moveGroups)
            {
                if (!AvailableGroupsForMove.Contains(g))
                    AvailableGroupsForMove.Add(g);
            }
        }


        private async Task LoadSettingsAsync()
        {
            MultiRobloxEnabled = await _settingsService.GetMultiRobloxEnabledAsync();
            PlaceId = await _settingsService.GetPlaceIdAsync();
            JobId = await _settingsService.GetJobIdAsync();
            LaunchData = await _settingsService.GetLaunchDataAsync();
            
            BackgroundImagePath = await _settingsService.GetSettingAsync<string>("BackgroundImagePath");
            BackgroundImageStretch = await _settingsService.GetSettingAsync<string>("BackgroundImageStretch") ?? "UniformToFill";
            BackgroundImageAlignment = await _settingsService.GetSettingAsync<string>("BackgroundImageAlignment") ?? "Center";
            BackgroundImageOpacity = await _settingsService.GetSettingAsync<double>("BackgroundImageOpacity", 1.0);
        }

        private void SettingsViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SettingsViewModel.BackgroundImagePath):
                    BackgroundImagePath = SettingsViewModel.BackgroundImagePath;
                    break;
                case nameof(SettingsViewModel.BackgroundImageStretch):
                    BackgroundImageStretch = SettingsViewModel.BackgroundImageStretch;
                    break;
                case nameof(SettingsViewModel.BackgroundImageAlignment):
                    BackgroundImageAlignment = SettingsViewModel.BackgroundImageAlignment;
                    break;
                case nameof(SettingsViewModel.BackgroundImageOpacity):
                    BackgroundImageOpacity = SettingsViewModel.BackgroundImageOpacity;
                    break;
                case nameof(SettingsViewModel.PlaceId):
                    PlaceId = SettingsViewModel.PlaceId;
                    break;
                case nameof(SettingsViewModel.JobId):
                    JobId = SettingsViewModel.JobId;
                    break;
                case nameof(SettingsViewModel.LaunchData):
                    LaunchData = SettingsViewModel.LaunchData;
                    break;
            }
        }

        // ── Commands ─────────────────────────────────────────────────────────

        [RelayCommand]
        private async Task AddAccountAsync()
        {
            // This is the manual login option
            try
            {
                StatusMessage = "Opening Roblox login...";
                IsLoading = true;

                var browserService = App.GetService<IBrowserService>();
                var accountService = App.GetService<IAccountService>();

                var loginInfo = await browserService.AcquireRobloxLoginInfoAsync();
                if (loginInfo == null || string.IsNullOrWhiteSpace(loginInfo.SecurityToken))
                {
                    StatusMessage = "Login cancelled or no cookie detected.";
                    return;
                }

                StatusMessage = "Adding account...";
                var account = await accountService.LoginWithCookieAsync(loginInfo.SecurityToken.Trim());
                if (account == null)
                {
                    StatusMessage = "Failed to add account. Invalid cookie?";
                    return;
                }

                // Apply captured password
                account.Password = loginInfo.Password;
                await accountService.UpdateAccountAsync(account);

                StatusMessage = $"Added {account.Username}";
                await LoadAccountsAsync();
                await LoadGroupsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add account");
                StatusMessage = "Error adding account";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AddAccountUserPassAsync()
        {
            try
            {
                var prompt = new PromptWindow("Add Account", "Enter one or more User:Pass lines (e.g. user:pass)");
                if (prompt.ShowDialog() == true)
                {
                    var input = prompt.InputText ?? string.Empty;
                    var lines = input
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.Trim())
                        .Where(l => l.Contains(":"))
                        .ToList();

                    if (lines.Count == 0)
                    {
                        StatusMessage = "No valid lines. Use user:pass per line.";
                        return;
                    }

                    IsLoading = true;
                    int success = 0, fail = 0, skipped = 0;
                    var existingUsers = new HashSet<string>(
                        (await _accountService.GetAccountsAsync()).Select(a => a.Username),
                        StringComparer.OrdinalIgnoreCase);
                    foreach (var line in lines)
                    {
                        var parts = line.Split(':', 2);
                        var username = parts[0].Trim();
                        var password = parts[1].Trim();
                        if (existingUsers.Contains(username))
                        {
                            skipped++;
                            continue;
                        }
                        StatusMessage = $"Logging in via browser as {username}...";
                        var account = await _accountService.LoginAsync(username, password, keepBrowserOpenUntilSuccess: true);
                        if (account != null)
                        {
                            existingUsers.Add(username);
                            success++;
                        }
                        else
                        {
                            fail++;
                        }
                    }

                    await LoadAccountsAsync();
                    await LoadGroupsAsync();
                    StatusMessage = $"Added {success}, {fail} failed, {skipped} skipped (duplicates)";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add account via User:Pass");
                StatusMessage = "Error adding account";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AddAccountCookieAsync()
        {
            try
            {
                var prompt = new PromptWindow("Add Account", "Enter .ROBLOSECURITY cookie");
                if (prompt.ShowDialog() == true)
                {
                    var cookie = prompt.InputText.Trim();
                    if (string.IsNullOrWhiteSpace(cookie)) return;

                    // Clean up cookie if it has the full .ROBLOSECURITY= prefix
                    if (cookie.Contains(".ROBLOSECURITY="))
                    {
                        var match = Regex.Match(cookie, @"\.ROBLOSECURITY=([^;]+)");
                        if (match.Success) cookie = match.Groups[1].Value;
                    }

                    StatusMessage = "Validating cookie...";
                    IsLoading = true;

                    var account = await _accountService.LoginWithCookieAsync(cookie);
                    if (account != null)
                    {
                        StatusMessage = $"Added {account.Username}";
                        await LoadAccountsAsync();
                        await LoadGroupsAsync();
                    }
                    else
                    {
                        StatusMessage = "Invalid cookie.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add account via Cookie");
                StatusMessage = "Error adding account";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RemoveAccountAsync(Account? account)
        {
            if (account == null) return;

            try
            {
                await _accountService.DeleteAccountAsync(account.Id);
                Accounts.Remove(account);
                StatusMessage = $"Removed account: {account.Username}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove account {Username}", account.Username);
                StatusMessage = "Error removing account";
            }
        }

        [RelayCommand]
        private async Task EditAccountAsync(Account? account)
        {
            if (account == null) return;

            // TODO: Open Edit Account dialog
            StatusMessage = "Edit account feature coming soon";
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task LaunchAccountAsync(Account? account)
        {
            if (account == null) return;

            try
            {
                StatusMessage = $"Launching {account.Username}...";

                if (!string.IsNullOrEmpty(PlaceId) && long.TryParse(PlaceId, out var placeId))
                {
                    var jobIdValue = string.IsNullOrEmpty(JobId) ? null : JobId;
                    var success = await _gameService.JoinGameAsync(account, placeId, jobIdValue, LaunchData);
                    if (success)
                    {
                        StatusMessage = $"Launched {account.Username}";
                        
                        // Wait briefly to detect process and update status
                        var startWaitUntil = DateTime.Now.AddSeconds(3);
                        while (DateTime.Now < startWaitUntil)
                        {
                            if (await _gameService.IsGameRunningAsync(account))
                            {
                                account.Status = AccountStatus.InGame;
                                if (account.InGameSince == null)
                                    account.InGameSince = DateTime.Now;
                                break;
                            }
                            await Task.Delay(250);
                        }
                        
                        await UpdateActiveAccountsCountAsync();
                        UpdateInGameUptimeText();
                    }
                    else
                    {
                        StatusMessage = $"Failed to launch {account.Username}";
                    }
                }
                else
                {
                    var success = await _browserService.LaunchBrowserAsync(account);
                    StatusMessage = success
                        ? $"Opened browser for {account.Username}"
                        : $"Failed to open browser for {account.Username}";
                }

                account.LastUsed = DateTime.Now;
                await _accountService.UpdateAccountAsync(account);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to launch account {Username}", account.Username);
                StatusMessage = $"Error launching {account.Username}";
            }
        }

        [RelayCommand]
        private async Task RefreshAccountAsync(Account? account)
        {
            if (account == null) return;

            try
            {
                StatusMessage = $"Refreshing {account.Username}...";
                await _accountService.RefreshAccountAsync(account);
                StatusMessage = $"Refreshed {account.Username}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh account {Username}", account.Username);
                StatusMessage = $"Error refreshing {account.Username}";
            }
        }

        [RelayCommand]
        private async Task RefreshAllAccountsAsync()
        {
            try
            {
                StatusMessage = "Refreshing all accounts...";
                IsLoading = true;

                var tasks = Accounts.Select(a => _accountService.RefreshAccountAsync(a));
                await Task.WhenAll(tasks);

                StatusMessage = "All accounts refreshed";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh all accounts");
                StatusMessage = "Error refreshing accounts";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task LaunchSelectedAccountsAsync()
        {
            if (SelectedAccounts.Count == 0)
            {
                StatusMessage = "No accounts selected";
                return;
            }

            try
            {
                StatusMessage = $"Launching {SelectedAccounts.Count} account(s)...";
                IsLoading = true;

                // FIX: Use JoinGameAsync instead of the non-existent LaunchGameAsync.
                // Parse PlaceId once outside the loop — skip launch if invalid.
                if (!long.TryParse(PlaceId, out var placeId))
                {
                    StatusMessage = "Invalid Place ID — cannot launch selected accounts.";
                    return;
                }

                var jobIdValue = string.IsNullOrEmpty(JobId) ? null : JobId;
                var launchDataValue = string.IsNullOrEmpty(LaunchData) ? null : LaunchData;

                var gameService = App.GetService<IGameService>();
                var delaySeconds = SettingsViewModel?.JoinDelaySeconds ?? 0;
                if (delaySeconds < 0) delaySeconds = 0;
                var delayMs = delaySeconds * 1000;

                // Snapshot selection to avoid "Collection was modified" during async operations
                var selectedSnapshot = SelectedAccounts.ToList();

                // Validate launch data if provided
                if (!string.IsNullOrEmpty(launchDataValue))
                {
                    try
                    {
                        var trimmedData = launchDataValue.Trim();
                        if (!trimmedData.StartsWith("{"))
                        {
                            // Simple validation for private server codes (alphanumeric, typical length)
                            if (trimmedData.Length < 4 || trimmedData.Length > 50 || !trimmedData.All(c => char.IsLetterOrDigit(c)))
                            {
                                StatusMessage = "Invalid launch data format. Private server codes should be alphanumeric.";
                                return;
                            }
                            
                            // Warn about using private server codes with multiple accounts
                            if (selectedSnapshot.Count > 1)
                            {
                                StatusMessage = "Warning: Using private server codes with multiple accounts may cause conflicts. Some accounts may fail to join.";
                            }
                        }
                        else
                        {
                            // Try to parse as JSON to validate format
                            JToken.Parse(trimmedData);
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = "Invalid launch data format. Please check your launch data.";
                        _logger.LogWarning(ex, "Invalid launch data format: {LaunchData}", launchDataValue);
                        return;
                    }
                }
                
                // Ensure each selected account has a valid cookie; auto-login via browser when possible
                var browserService = App.GetService<IBrowserService>();
                foreach (var acct in selectedSnapshot)
                {
                    if (string.IsNullOrEmpty(acct.SecurityToken) && !string.IsNullOrEmpty(acct.Username) && !string.IsNullOrEmpty(acct.Password))
                    {
                        StatusMessage = $"Preparing {acct.Username}...";
                        var cookie = await browserService.LoginAndGetCookieAsync(acct.Username, acct.Password);
                        if (!string.IsNullOrEmpty(cookie))
                        {
                            acct.SecurityToken = cookie;
                            acct.IsValid = true;
                            await _accountService.UpdateAccountAsync(acct);
                            await Task.Delay(200); // small spacing between logins
                        }
                    }
                }

                var accountsToLaunch = selectedSnapshot.Where(a => !string.IsNullOrEmpty(a.SecurityToken)).ToList();
                if (accountsToLaunch.Count == 0)
                {
                    StatusMessage = "Selected accounts are missing cookies — login first.";
                    return;
                }
                
                async Task<bool> LaunchOne(Account acct)
                {
                    try
                    {
                        var ok = await gameService.JoinGameAsync(acct, placeId, jobIdValue, launchDataValue);
                        if (!ok)
                        {
                            await Task.Delay(500);
                            ok = await gameService.JoinGameAsync(acct, placeId, jobIdValue, launchDataValue);
                            if (!ok)
                            {
                                // Fallback through web to trigger protocol handler
                                var browserService = App.GetService<IBrowserService>();
                                ok = await browserService.JoinGameViaWebAsync(acct, placeId, jobIdValue, launchDataValue);
                            }
                        }
                        
                        if (!ok && !string.IsNullOrEmpty(launchDataValue))
                        {
                            _logger.LogWarning("Failed to launch account {Username} with launch data. The launch data may be invalid or expired.", acct.Username);
                        }
                        
                        return ok;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception during launch of account {Username}", acct.Username);
                        return false;
                    }
                }

                // Use sequential launching which is more reliable with roblox-player protocol
                int successCount = 0;
                var spacingMs = delayMs == 0 ? 300 : delayMs;
                foreach (var acct in accountsToLaunch)
                {
                    try
                    {
                        var launched = await LaunchOne(acct);
                        if (launched) successCount++;

                        // Wait briefly to let Roblox bootstrapper start before the next launch
                        var startWaitUntil = DateTime.UtcNow.AddSeconds(3);
                        while (DateTime.UtcNow < startWaitUntil)
                        {
                            bool running = await gameService.IsGameRunningAsync(acct);
                            if (running)
                            {
                                acct.Status = AccountStatus.InGame;
                                if (acct.InGameSince == null)
                                    acct.InGameSince = DateTime.Now;
                                break;
                            }
                            await Task.Delay(250);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to launch account {Username}", acct.Username);
                    }
                    await Task.Delay(spacingMs);
                }
                StatusMessage = $"Launched {successCount} out of {accountsToLaunch.Count} account(s)";
                
                // Immediately update sidebar UI
                await UpdateActiveAccountsCountAsync();
                UpdateInGameUptimeText();

                foreach (var account in accountsToLaunch)
                {
                    account.LastUsed = DateTime.Now;
                    await _accountService.UpdateAccountAsync(account);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to launch selected accounts");
                StatusMessage = "Error launching accounts";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task OpenBrowserAsync(Account account)
        {
            if (account == null)
            {
                StatusMessage = "No account provided";
                return;
            }

            try
            {
                StatusMessage = $"Opening browser for {account.Username}...";
                IsLoading = true;
                var browserService = App.GetService<IBrowserService>();
                var ok = await browserService.LaunchBrowserAsync(account);
                StatusMessage = ok
                    ? $"Browser opened for {account.Username}"
                    : $"Failed to open browser for {account.Username}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open browser for account {Username}", account.Username);
                StatusMessage = "Error opening browser";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task LaunchSelectedAccountAsync()
        {
            var selectedAccount = SelectedAccounts.FirstOrDefault(a => a.IsValid);
            if (selectedAccount == null)
            {
                StatusMessage = "No valid account selected to launch";
                return;
            }

            try
            {
                StatusMessage = $"Launching {selectedAccount.Username}...";
                IsLoading = true;

                var jobIdValue = string.IsNullOrEmpty(JobId) ? null : JobId;
                var launchDataValue = string.IsNullOrEmpty(LaunchData) ? null : LaunchData;

                var success = await _gameService.JoinGameAsync(selectedAccount, long.Parse(PlaceId), jobIdValue, launchDataValue);
                StatusMessage = success
                    ? $"Launched {selectedAccount.Username}"
                    : $"Failed to launch {selectedAccount.Username}";

                selectedAccount.LastUsed = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to launch selected account");
                StatusMessage = "Error launching account";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RemoveSelectedAccountsAsync()
        {
            var selectedAccounts = SelectedAccounts.ToList();
            if (selectedAccounts.Count == 0)
            {
                StatusMessage = "No accounts selected to remove";
                return;
            }

            try
            {
                StatusMessage = $"Removing {selectedAccounts.Count} account(s)...";
                IsLoading = true;

                // Delete accounts one by one using existing DeleteAccountAsync method
                foreach (var account in selectedAccounts)
                {
                    await _accountService.DeleteAccountAsync(account.Id);
                }
                
                StatusMessage = $"Removed {selectedAccounts.Count} account(s)";
                await LoadAccountsAsync();
                await LoadGroupsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove selected accounts");
                StatusMessage = "Error removing accounts";
            }
            finally
            {
                IsLoading = false;
            }
        }




        [RelayCommand]
        private async Task ImportAccountsAsync()
        {
            StatusMessage = "Import accounts feature coming soon";
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task BulkImportUserPassAsync()
        {
            try
            {
                var bulkImportWindow = new BulkImportWindow();
                var viewModel = bulkImportWindow.DataContext as ViewModels.BulkImportViewModel;
                if (viewModel != null)
                {
                    viewModel.SetImportType("UserPass");
                }
                
                bulkImportWindow.Owner = Application.Current.MainWindow;
                bulkImportWindow.ShowDialog();
                
                // Refresh accounts after import
                await LoadAccountsAsync();
                StatusMessage = "Bulk import completed";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening bulk user:pass import");
                StatusMessage = "Error opening bulk import";
            }
        }

        [RelayCommand]
        private async Task BulkImportCookieAsync()
        {
            try
            {
                var bulkImportWindow = new BulkImportWindow();
                var viewModel = bulkImportWindow.DataContext as ViewModels.BulkImportViewModel;
                if (viewModel != null)
                {
                    viewModel.SetImportType("Cookie");
                }
                
                bulkImportWindow.Owner = Application.Current.MainWindow;
                bulkImportWindow.ShowDialog();
                
                // Refresh accounts after import
                await LoadAccountsAsync();
                StatusMessage = "Bulk cookie import completed";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening bulk cookie import");
                StatusMessage = "Error opening bulk cookie import";
            }
        }

        [RelayCommand]
        private async Task ExportAccountsAsync()
        {
            StatusMessage = "Export accounts feature coming soon";
            await Task.CompletedTask;
        }

        // ── Property change handlers ─────────────────────────────────────────

        partial void OnSelectedGroupChanged(string value) => UpdateDisplayItems();

        partial void OnSearchTextChanged(string value) => UpdateDisplayItems();


        // ── Context Menu Commands ───────────────────────────────────────────

        [RelayCommand]
        private async Task CopyUsername(Account? account)
        {
            if (account != null)
                await ClipboardHelper.SetTextAsync(account.Username);
        }

        [RelayCommand]
        private async Task CopyPassword(Account? account)
        {
            if (account != null)
            {
                if (!string.IsNullOrEmpty(account.Password))
                {
                    await ClipboardHelper.SetTextAsync(account.Password);
                    StatusMessage = $"Copied password for {account.Username}";
                }
                else
                {
                    StatusMessage = "No password stored for this account.";
                }
            }
        }

        [RelayCommand]
        private async Task CopyCombo(Account? account)
        {
            if (account != null)
            {
                string pwd = account.Password ?? "";
                await ClipboardHelper.SetTextAsync($"{account.Username}:{pwd}");
                if (string.IsNullOrEmpty(pwd))
                    StatusMessage = $"Warning: Copied {account.Username}: (Empty Password)";
                else
                    StatusMessage = $"Copied {account.Username} combo";
            }
        }

        [RelayCommand]
        private async Task CopyProfile(Account? account)
        {
            if (account != null)
                await ClipboardHelper.SetTextAsync($"https://www.roblox.com/users/{account.UserId}/profile");
        }

        [RelayCommand]
        private async Task CopyUserId(Account? account)
        {
            if (account != null)
                await ClipboardHelper.SetTextAsync(account.UserId.ToString());
        }

        [RelayCommand]
        private void SortAlphabetically()
        {
            var sorted = Accounts.OrderBy(a => a.Username).ToList();
            Accounts.Clear();
            foreach (var acc in sorted) Accounts.Add(acc);
            UpdateDisplayItems();
            StatusMessage = "Sorted alphabetically";
        }

        [RelayCommand]
        private async Task QuickLogInAsync(Account? account)
        {
            if (account == null) return;
            
            var prompt = new BloxManager.Views.PromptWindow("Quick Log In", "Enter the 6-character code:");
            if (prompt.ShowDialog() == true && !string.IsNullOrWhiteSpace(prompt.InputText))
            {
                StatusMessage = "Approving quick login...";
                bool success = await _browserService.ApproveQuickLoginAsync(account, prompt.InputText.Trim());
                StatusMessage = success ? "Quick login approved!" : "Failed to approve quick login.";
            }
        }

        [RelayCommand]
        private void Help()
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/JakeBx99/Roblox-Account-Manager",
                UseShellExecute = true
            });
        }

        [RelayCommand]
        public async Task EditDescriptionAsync(Account? account)
        {
            if (account == null) return;

            var prompt = new BloxManager.Views.PromptWindow("Edit Description", "Enter a new description for this account:", account.Description);
            if (prompt.ShowDialog() == true)
            {
                account.Description = prompt.InputText;
                await _accountService.UpdateAccountAsync(account);
                StatusMessage = $"Updated description for {account.Username}";
            }
        }

        // Password editing removed in favor of automatic tracking during login.


        [RelayCommand]
        private async Task CreateGroupAsync(Account? account)
        {
            var prompt = new BloxManager.Views.PromptWindow("Create Group", "Enter a name for the new group:");
            if (prompt.ShowDialog() == true && !string.IsNullOrWhiteSpace(prompt.InputText))
            {
                var newGroup = prompt.InputText.Trim();
                if (!Groups.Contains(newGroup))
                {
                    Groups.Add(newGroup);
                    // Optionally, if an account was right-clicked, immediately move it to the new group
                    if (account != null)
                    {
                        account.Group = newGroup;
                        await _accountService.UpdateAccountAsync(account);
                        UpdateDisplayItems();
                    }

                    StatusMessage = $"Created group: {newGroup}";
                }
                else
                {
                    StatusMessage = "Group already exists.";
                }
            }
        }

        [RelayCommand]
        private async Task MoveAccountToGroupAsync(object parameter)
        {
            // The command parameter here will be an object array from the MultiParamConverter containing {DataContext, GroupName}
            // However, due to the way ContextMenu item templating works, it's often easier to just track the selected items.
            // But since this receives the parameters:
            if (parameter is object[] values && values.Length == 2)
            {
                var groupName = values[1] as string;
                var dataContext = values[0] as MainViewModel;

                // Move all selected accounts to this group
                if (groupName != null && SelectedAccounts.Any())
                {
                    foreach (var acc in SelectedAccounts)
                    {
                        acc.Group = groupName;
                        await _accountService.UpdateAccountAsync(acc);
                    }
                    UpdateDisplayItems();
                    StatusMessage = $"Moved {SelectedAccounts.Count} account(s) to {groupName}";
                }
            }
        }

        [RelayCommand]
        public void ToggleGroupExpansion(AccountGroup? group)
        {
            if (group != null)
            {
                System.Diagnostics.Debug.WriteLine($"ToggleGroupExpansion called for group: {group.Name}, current IsExpanded: {group.IsExpanded}");
                
                group.IsExpanded = !group.IsExpanded;
                
                System.Diagnostics.Debug.WriteLine($"After toggle - new IsExpanded: {group.IsExpanded}");
                
                UpdateDisplayItems();
                
                System.Diagnostics.Debug.WriteLine("UpdateDisplayItems called");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ToggleGroupExpansion called with null group");
            }
        }

        [RelayCommand]
        public async Task DeleteGroupAsync(AccountGroup? group)
        {
            if (group == null) return;
            
            // Prevent deletion of the Default group
            if (group.Name == "Default")
            {
                StatusMessage = "Cannot delete the Default group";
                return;
            }

            try
            {
                // Move all accounts in this group to "Default"
                foreach (var account in group.Accounts)
                {
                    account.Group = "Default";
                    await _accountService.UpdateAccountAsync(account);
                }

                // Save all account changes
                await _accountService.SaveAccountsAsync();

                // Remove the group from the Groups collection
                Groups.Remove(group.Name);
                
                // Update display items to reflect the changes
                UpdateDisplayItems();
                StatusMessage = $"Deleted group: {group.Name}";
            }
            catch (Exception)
            {
                _logger.LogError("Failed to delete group {GroupName}", group.Name);
                StatusMessage = "Error deleting group";
            }
        }

        public void EditGroupName(AccountGroup group, string newName)
        {
            if (group == null || string.IsNullOrWhiteSpace(newName)) return;

            try
            {
                // Prevent renaming to "Default" or existing group names
                if (newName == "Default" || Groups.Contains(newName))
                {
                    StatusMessage = "Group name already exists or cannot be 'Default'";
                    return;
                }

                var oldName = group.Name;
                
                // Update all accounts in this group to use the new name
                foreach (var account in Accounts.Where(a => a.Group == oldName))
                {
                    account.Group = newName;
                    _ = _accountService.UpdateAccountAsync(account);
                }

                // Update the Groups collection
                var index = Groups.IndexOf(oldName);
                if (index >= 0)
                {
                    Groups[index] = newName;
                }

                // Update the AccountGroup name
                group.Name = newName;
                
                // Update display items
                UpdateDisplayItems();
                StatusMessage = $"Renamed group from '{oldName}' to '{newName}'";
                
                // Save the changes
                _ = SavePersistentGroupsAsync();
            }
            catch (Exception)
            {
                _logger.LogError("Failed to rename group from {OldName} to {NewName}", group.Name, newName);
                StatusMessage = "Error renaming group";
            }
        }
    }
}
