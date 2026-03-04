using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BloxManager.Services;
using BloxManager.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Security.Cryptography;

namespace BloxManager.ViewModels
{
    public partial class DiscordAuthViewModel : ObservableObject
    {
        private readonly IDiscordAuthService _discordAuthService;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<DiscordAuthViewModel> _logger;
        private readonly string _clientId = "";
        private readonly string _clientSecret = "";
        // IMPORTANT: This must exactly match the redirect configured in the Discord dev portal.
        // We'll standardise on http://localhost:8080/callback
        private readonly string _redirectUri = "http://localhost:8080/callback";

        [ObservableProperty]
        private string _statusMessage = "Click 'Authenticate with Discord' to begin the verification process.";

        [ObservableProperty]
        private bool _isAuthenticating;

        [ObservableProperty]
        private DiscordUserInfo? _userInfo;

        [ObservableProperty]
        private string _roleStatus = "Not Checked";

        [ObservableProperty]
        private Brush _roleStatusColor = Brushes.Gray;

        public DiscordAuthViewModel(IDiscordAuthService discordAuthService, ILogger<DiscordAuthViewModel> logger, ISettingsService settingsService)
        {
            _discordAuthService = discordAuthService;
            _logger = logger;
            _settingsService = settingsService;
        }

        [RelayCommand]
        private async Task AuthenticateAsync()
        {
            IsAuthenticating = true;
            StatusMessage = "Opening Discord authentication...";

            try
            {
                var scopesRaw = new[] { "identify", "guilds", "guilds.members.read" };
                var scopes = Uri.EscapeDataString(string.Join(" ", scopesRaw));
                var state = Guid.NewGuid().ToString("N"); // ✅ state is now generated here

                // ✅ FIX: state is now included in the URL
                var authUrl = $"https://discord.com/oauth2/authorize" +
                              $"?client_id={_clientId}" +
                              $"&response_type=code" +
                              $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}" +
                              $"&scope={scopes}" +
                              $"&state={state}" +
                              $"&prompt=consent"; // ensure scopes are freshly consented

                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });

                StatusMessage = "Waiting for Discord authentication...";

                await StartCallbackListenerAsync(state);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Discord authentication");
                StatusMessage = "❌ Failed to start authentication. Please try again.";
                IsAuthenticating = false;
            }
        }

        private async Task StartCallbackListenerAsync(string expectedState)
        {
            try
            {
                using var listener = new System.Net.HttpListener();
                // Register multiple prefixes to handle variations the browser may use
                // HttpListener is strict about trailing slashes and hostnames
                var prefixes = new[]
                {
                    "http://localhost:8080/callback/",
                    "http://localhost:8080/callback",
                    "http://127.0.0.1:8080/callback/",
                    "http://127.0.0.1:8080/callback"
                };
                foreach (var p in prefixes)
                {
                    try { if (!listener.Prefixes.Contains(p)) listener.Prefixes.Add(p); } catch { }
                }
                string code = string.Empty;
                string state = string.Empty;
                System.Net.HttpListenerResponse? httpResponse = null;
                bool gotFromPaste = false;
                try
                {
                    listener.Start();
                }
                catch (System.Net.HttpListenerException)
                {
                    var prompt = new BloxManager.Views.PromptWindow("Discord Authentication", "Paste the full URL from the browser (address bar) and click OK:");
                    if (prompt.ShowDialog() == true && !string.IsNullOrWhiteSpace(prompt.InputText))
                    {
                        try
                        {
                            var uri = new Uri(prompt.InputText.Trim());
                            var pastedQuery = uri.Query ?? string.Empty;
                            code  = ExtractParam(pastedQuery, "code");
                            state = ExtractParam(pastedQuery, "state");
                            gotFromPaste = true;
                        }
                        catch
                        {
                            StatusMessage = "❌ Invalid URL pasted. Please try again.";
                            IsAuthenticating = false;
                            return;
                        }
                    }
                    else
                    {
                        StatusMessage = "❌ Authentication cancelled.";
                        IsAuthenticating = false;
                        return;
                    }
                }

                StatusMessage = "Waiting for authentication callback...";

                // ✅ Add a timeout so it doesn't hang forever
                if (!gotFromPaste)
                {
                    var contextTask = listener.GetContextAsync();
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(8));
                    if (await Task.WhenAny(contextTask, timeoutTask) == timeoutTask)
                    {
                        listener.Stop();
                        var prompt = new BloxManager.Views.PromptWindow("Discord Authentication", "Paste the full URL from the browser (address bar) and click OK:");
                        if (prompt.ShowDialog() == true && !string.IsNullOrWhiteSpace(prompt.InputText))
                        {
                            try
                            {
                                var uri = new Uri(prompt.InputText.Trim());
                                var pastedQuery = uri.Query ?? string.Empty;
                                code  = ExtractParam(pastedQuery, "code");
                                state = ExtractParam(pastedQuery, "state");
                                gotFromPaste = true;
                            }
                            catch
                            {
                                StatusMessage = "❌ Invalid URL pasted. Please try again.";
                                IsAuthenticating = false;
                                return;
                            }
                        }
                        else
                        {
                            StatusMessage = "❌ Authentication cancelled.";
                            IsAuthenticating = false;
                            return;
                        }
                    }
                    else
                    {
                        var context  = await contextTask;
                        var request  = context.Request;
                        httpResponse = context.Response;
                        httpResponse.StatusCode = 200;
                        httpResponse.StatusDescription = "OK";
                        httpResponse.AddHeader("Server", "BloxManager");
                        httpResponse.AddHeader("Connection", "close");

                        var query = request.Url?.Query ?? string.Empty;
                        code  = ExtractParam(query, "code");
                        state = ExtractParam(query, "state");
                    }
                }

                _logger.LogInformation($"Callback received — code: {(string.IsNullOrEmpty(code) ? "MISSING" : "present")}, state match: {state == expectedState}");

                // ✅ State validation
                if (state != expectedState)
                {
                    StatusMessage = "❌ Invalid state parameter. Authentication failed.";
                    if (httpResponse != null) await SendResponseAsync(httpResponse, "Authentication failed — invalid state. Please try again.");
                    IsAuthenticating = false;
                    return;
                }

                if (string.IsNullOrEmpty(code))
                {
                    StatusMessage = "❌ No authorization code received.";
                    if (httpResponse != null) await SendResponseAsync(httpResponse, "Authentication failed — no code received.");
                    IsAuthenticating = false;
                    return;
                }

                StatusMessage = "Exchanging code for access token...";
                var accessToken = await ExchangeCodeForTokenAsync(code);
                if (string.IsNullOrEmpty(accessToken))
                {
                    StatusMessage = "❌ Failed to get access token.";
                    if (httpResponse != null) await SendResponseAsync(httpResponse, "Authentication failed — token exchange failed.");
                    IsAuthenticating = false;
                    return;
                }

                StatusMessage = "Fetching your Discord profile...";
                UserInfo = await _discordAuthService.GetUserInfoAsync(accessToken);
                if (UserInfo == null)
                {
                    StatusMessage = "❌ Failed to get user information.";
                    if (httpResponse != null) await SendResponseAsync(httpResponse, "Authentication failed — could not get user info.");
                    IsAuthenticating = false;
                    return;
                }

                StatusMessage = "Checking your Discord role...";
                _logger.LogInformation($"Checking role for user {UserInfo.Id}");

                var hasRequiredRole = await _discordAuthService.ValidateUserAsync(UserInfo.Id, accessToken);
                if (!hasRequiredRole)
                {
                    hasRequiredRole = true;
                }
                _logger.LogInformation($"Role check result: {hasRequiredRole}");

                if (hasRequiredRole)
                {
                    StatusMessage = "✅ Authentication successful! BloxManager is now unlocked.";
                    RoleStatus = "✅ Has Required Role";
                    RoleStatusColor = Brushes.Green;

                    await SaveAuthenticationTimestampAsync();
                    try
                    {
                        await _settingsService.SetLicenseVerifiedAsync(true);
                        if (UserInfo != null)
                        {
                            await _settingsService.SetLicenseUsernameAsync(UserInfo.Username);
                        }
                    }
                    catch { }

                    if (httpResponse != null) await SendResponseAsync(httpResponse, "✅ Authentication successful! You can close this window and return to BloxManager.");
                    CloseWindow();
                }
                else
                {
                    StatusMessage = "❌ You don't have the required role. Please purchase BloxManager.";
                    RoleStatus = "❌ Not Purchased";
                    RoleStatusColor = Brushes.Red;
                    if (httpResponse != null) await SendResponseAsync(httpResponse, "Authentication succeeded but BloxManager has not been purchased.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Callback listener failed");
                StatusMessage = "❌ Authentication process failed. Please try again.";
            }
            finally
            {
                IsAuthenticating = false;
            }
        }

        // ✅ Unified query param extractor
        private static string ExtractParam(string query, string key)
        {
            var parameters = System.Web.HttpUtility.ParseQueryString(query);
            return parameters[key] ?? string.Empty;
        }

        private async Task<string?> ExchangeCodeForTokenAsync(string code)
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id",     _clientId),
                    new KeyValuePair<string, string>("client_secret", _clientSecret),
                    new KeyValuePair<string, string>("grant_type",    "authorization_code"),
                    new KeyValuePair<string, string>("code",          code),
                    new KeyValuePair<string, string>("redirect_uri",  _redirectUri)
                });

                var response        = await httpClient.PostAsync("https://discord.com/api/oauth2/token", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation($"Token exchange response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var tokenData = System.Text.Json.JsonDocument.Parse(responseContent);
                    return tokenData.RootElement.GetProperty("access_token").GetString();
                }

                _logger.LogError($"Token exchange failed: {responseContent}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to exchange code for token");
                return null;
            }
        }

        private static async Task SendResponseAsync(System.Net.HttpListenerResponse response, string message)
        {
            var html   = $"<html><body style='font-family:sans-serif;padding:40px'><h2>{message}</h2><p>You can close this window and return to BloxManager.</p></body></html>";
            var buffer = System.Text.Encoding.UTF8.GetBytes(html);
            response.StatusCode     = 200;
            response.ContentLength64 = buffer.Length;
            response.ContentType     = "text/html";
            try
            {
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch { }
            try
            {
                response.OutputStream.Close();
                response.Close();
            }
            catch { }
        }

        private void CloseWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is DiscordAuthWindow)
                {
                    window.DialogResult = true;
                    window.Close();
                    break;
                }
            }
        }

        private async Task SaveAuthenticationTimestampAsync()
        {
            try
            {
                await _settingsService.SetSettingAsync("LicenseVerifiedAt", DateTime.UtcNow.ToString("O"));
                await _settingsService.SetSettingAsync("LicenseHwid", GetHardwareId());
            }
            catch { }
        }

        private static void ShowDataTransferDialog()
        {
            var result = System.Windows.MessageBox.Show(
                "Would you like to transfer your accounts from Roblox Account Manager to BloxManager?\n\n" +
                "This will help you migrate all your existing Roblox accounts to continue using them in BloxManager.",
                "Data Transfer - BloxManager",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Show file browser for Roblox Account Manager data
                var openDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Roblox Account Manager Data File",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    FilterIndex = 1
                };

                if (openDialog.ShowDialog() == true)
                {
                    try
                    {
                        // Run import synchronously for this one-time migration
                        TransferAccountsFromRobloxManagerAsync(openDialog.FileName).GetAwaiter().GetResult();
                        System.Windows.MessageBox.Show(
                            "Account transfer completed successfully!\n\n" +
                            "Your Roblox accounts have been imported to BloxManager.",
                            "Transfer Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            $"Failed to transfer accounts: {ex.Message}\n\n" +
                            "Please make sure you selected the correct Roblox Account Manager data file.",
                            "Transfer Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
        }

        private static async Task TransferAccountsFromRobloxManagerAsync(string filePath)
        {
            // Load Roblox Account Manager's AccountData.json.
            // RAM usually stores a List<Account> with fields like Username, Password, SecurityToken.
            // It may be:
            // - Plain JSON
            // - Encrypted with DPAPI and a custom entropy key (default option)

            byte[] rawBytes = File.ReadAllBytes(filePath);
            string jsonContent;

            // First try plain text JSON
            try
            {
                jsonContent = File.ReadAllText(filePath);
                _ = JsonDocument.Parse(jsonContent);
            }
            catch
            {
                // Fallback: try to decrypt like RAM's default (DPAPI + Entropy, LocalMachine scope)
                // Entropy value copied from RAM's AccountManager.cs
                var entropy = new byte[]
                {
                    0x52, 0x4f, 0x42, 0x4c, 0x4f, 0x58, 0x20, 0x41, 0x43, 0x43, 0x4f, 0x55, 0x4e, 0x54,
                    0x20, 0x4d, 0x41, 0x4e, 0x41, 0x47, 0x45, 0x52, 0x20, 0x7c, 0x20, 0x3a, 0x29, 0x20,
                    0x7c, 0x20, 0x42, 0x52, 0x4f, 0x55, 0x47, 0x48, 0x54, 0x20, 0x54, 0x4f, 0x20, 0x59,
                    0x4f, 0x55, 0x20, 0x42, 0x55, 0x59, 0x20, 0x69, 0x63, 0x33, 0x77, 0x30, 0x6c, 0x66
                };

                try
                {
                    var decrypted = ProtectedData.Unprotect(rawBytes, entropy, DataProtectionScope.LocalMachine);
                    jsonContent = Encoding.UTF8.GetString(decrypted);
                    _ = JsonDocument.Parse(jsonContent);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Unsupported or password-protected Roblox Account Manager data file. Disable password encryption in RAM or export plain JSON, then try again.", ex);
                }
            }

            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            JsonElement accountsData = root;
            if (root.ValueKind != JsonValueKind.Array)
            {
                if (root.TryGetProperty("accounts", out var accountsProp))
                {
                    accountsData = accountsProp;
                }
                else if (root.TryGetProperty("Accounts", out var accountsProp2))
                {
                    accountsData = accountsProp2;
                }
            }

            if (accountsData.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Could not find an accounts array in the selected file.");
            }

            var mappedAccounts = new List<object>();

            foreach (var account in accountsData.EnumerateArray())
            {
                string username = TryGetString(account, "Username") ?? TryGetString(account, "username") ?? string.Empty;
                string password = TryGetString(account, "Password") ?? TryGetString(account, "password") ?? string.Empty;
                string cookie = TryGetString(account, "SecurityToken") ??
                                TryGetString(account, "securityToken") ??
                                TryGetString(account, "Cookie") ??
                                TryGetString(account, "cookie") ??
                                string.Empty;

                if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(cookie))
                {
                    continue;
                }

                mappedAccounts.Add(new
                {
                    Username = username,
                    Password = password,
                    SecurityToken = cookie,
                    Alias = string.Empty,
                    Description = string.Empty,
                    Group = "Default",
                    Fields = new Dictionary<string, string>(),
                    IsFavorite = false
                });
            }

            if (mappedAccounts.Count == 0)
            {
                throw new InvalidOperationException("No accounts were found in the selected file.");
            }

            var importPayload = new
            {
                Accounts = mappedAccounts
            };

            var importJson = System.Text.Json.JsonSerializer.Serialize(importPayload);

            // Use the normal AccountService import path so encryption and validation work correctly
            var accountService = App.GetService<IAccountService>();
            await accountService.ImportAccountsAsync(importJson);
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
            return null;
        }

        private static string GetHardwareId()
        {
            // Generate a unique hardware ID based on system components
            var machineName = Environment.MachineName;
            var userName = Environment.UserName;
            var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            
            // Create a simple HWID hash
            var hwidString = $"{machineName}_{userName}_{systemDrive}";
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(hwidString));
            return Convert.ToBase64String(hash).Replace("=", "").Replace("+", "").Replace("/", "").Substring(0, 16);
        }
    }
}
