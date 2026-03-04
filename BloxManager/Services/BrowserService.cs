using BloxManager.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BloxManager.Views;

namespace BloxManager.Services
{
    public class BrowserService : IBrowserService
    {
        private readonly ILogger<BrowserService> _logger;
        private readonly Dictionary<string, IBrowser> _browsers = new();
        private readonly Dictionary<string, IPage> _pages = new();

        private static readonly SemaphoreSlim _chromiumDownloadLock = new(1, 1);
        private static string? _cachedChromiumExecutablePath;

        public BrowserService(ILogger<BrowserService> logger)
        {
            _logger = logger;
        }

        public async Task<RobloxLoginInfo?> AcquireRobloxLoginInfoAsync(CancellationToken cancellationToken = default)
        {
            IBrowser? browser = null;
            IPage? page = null;
            string lastPassword = string.Empty;

            try
            {
                var chromiumExecutablePath = await EnsureChromiumAsync();

                var userDataDir = Path.Combine(
                    Path.GetTempPath(),
                    "BloxManager",
                    "AddAccount",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(userDataDir);

                var browserOptions = new LaunchOptions
                {
                    Headless = false,
                    DefaultViewport = null,
                    ExecutablePath = chromiumExecutablePath,
                    Args = new[]
                    {
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--no-first-run",
                        $"--user-data-dir={userDataDir}",
                        "--disable-blink-features=AutomationControlled"
                    }
                };

                browser = await Puppeteer.LaunchAsync(browserOptions);
                page = await browser.NewPageAsync();

                // Expose a function to capture the password
                await page.ExposeFunctionAsync("capturePassword", (string pwd) =>
                {
                    if (!string.IsNullOrEmpty(pwd)) lastPassword = pwd;
                    return true;
                });

                // Inject a script to listen for input in password fields
                await page.EvaluateFunctionOnNewDocumentAsync(@"() => {
                    const handleInput = (e) => {
                        if (e.target.type === 'password' || e.target.name === 'password') {
                            window.capturePassword(e.target.value);
                        }
                    };
                    document.addEventListener('input', handleInput, true);
                    document.addEventListener('change', handleInput, true);
                    document.addEventListener('blur', handleInput, true);
                }");
                
                // Track finished network requests to capture password from POST payloads (more reliable than initial Request)
                page.RequestFinished += async (sender, e) =>
                {
                    try
                    {
                        var url = new Uri(e.Request.Url);
                        if (e.Request.Method.ToString().Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                            (url.AbsolutePath == "/v2/login" || url.AbsolutePath == "/v2/signup"))
                        {
                            var postData = e.Request.PostData?.ToString();
                            if (!string.IsNullOrEmpty(postData))
                            {
                                try
                                {
                                    var jObj = JObject.Parse(postData);
                                    var pwd = jObj["password"]?.ToString();
                                    
                                    // Reliability check: Only capture if it looks like a standard username/password login
                                    var ctype = jObj["ctype"]?.ToString();
                                    if (!string.IsNullOrEmpty(pwd) && (ctype == null || ctype.Equals("username", StringComparison.OrdinalIgnoreCase)))
                                    {
                                        lastPassword = pwd;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                };

                await page.GoToAsync("https://www.roblox.com/login", new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }
                });

                // Wait for the .ROBLOSECURITY cookie to appear (user logs in or creates an account).
                var startedAt = DateTime.UtcNow;
                var timeout = TimeSpan.FromMinutes(10);
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (DateTime.UtcNow - startedAt > timeout)
                    {
                        return null;
                    }

                    try
                    {
                        // High-frequency polling for password (matching RAM's 100ms frequency)
                        try
                        {
                            var pwd = await page.EvaluateExpressionAsync<string>(@"
                                (function() {
                                    const p = document.getElementById('login-password') || 
                                              document.getElementById('signup-password') || 
                                              document.querySelector('input[type=password]') ||
                                              document.querySelector('input[name=password]');
                                    return p ? p.value : '';
                                })()
                            ");
                            if (!string.IsNullOrEmpty(pwd)) lastPassword = pwd;
                        }
                        catch { }

                        var cookies = await page.GetCookiesAsync("https://www.roblox.com/");
                        var sec = cookies.FirstOrDefault(c => string.Equals(c.Name, ".ROBLOSECURITY", StringComparison.OrdinalIgnoreCase));
                        if (sec != null && !string.IsNullOrWhiteSpace(sec.Value))
                        {
                            // Final capture attempt right before returning to avoid race conditions
                            try
                            {
                                var finalPwd = await page.EvaluateExpressionAsync<string>("document.getElementById('login-password')?.value || document.getElementById('signup-password')?.value || ''");
                                if (!string.IsNullOrEmpty(finalPwd)) lastPassword = finalPwd;
                                
                                // Give it one more tiny delay for in-flight requests to potentially settle lastPassword
                                if (string.IsNullOrEmpty(lastPassword)) await Task.Delay(100);
                            }
                            catch { }

                            _logger.LogInformation("Acquired .ROBLOSECURITY cookie and tracked password.");
                            return new RobloxLoginInfo
                            {
                                SecurityToken = sec.Value,
                                Password = lastPassword
                            };
                        }
                    }
                    catch
                    {
                        // Ignore transient errors while page is navigating.
                    }

                    await Task.Delay(100, cancellationToken);
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to acquire Roblox login info");
                return null;
            }
            finally
            {
                try
                {
                    if (page != null)
                    {
                        await page.CloseAsync();
                    }
                }
                catch { }

                try
                {
                    if (browser != null)
                    {
                        await browser.CloseAsync();
                    }
                }
                catch { }
            }
        }

        public async Task<bool> LaunchBrowserAsync(Account account)
        {
            try
            {
                if (!string.IsNullOrEmpty(account.BrowserTrackerId) && await IsBrowserRunningAsync(account))
                {
                    _logger.LogInformation($"Browser already running for account {account.Username}");
                    return true;
                }

                var chromiumExecutablePath = await EnsureChromiumAsync();

                var browserOptions = new LaunchOptions
                {
                    Headless = false,
                    DefaultViewport = null,
                    ExecutablePath = chromiumExecutablePath,
                    Args = new[]
                    {
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-accelerated-2d-canvas",
                        "--no-first-run",
                        "--no-zygote",
                        "--disable-gpu",
                        $"--user-data-dir={GetUserDataDirectory(account)}",
                        "--disable-web-security",
                        "--disable-features=VizDisplayCompositor",
                        "--disable-blink-features=AutomationControlled"
                    }
                };

                var browser = await Puppeteer.LaunchAsync(browserOptions);
                var page = await browser.NewPageAsync();

                // Set authentication cookie
                if (!string.IsNullOrEmpty(account.SecurityToken))
                {
                    await page.SetCookieAsync(new CookieParam
                    {
                        Name = ".ROBLOSECURITY",
                        Value = account.SecurityToken,
                        Domain = ".roblox.com"
                    });
                }

                // Navigate to Roblox
                await page.GoToAsync("https://www.roblox.com/home");

                var trackerId = Guid.NewGuid().ToString();
                account.BrowserTrackerId = trackerId;

                _browsers[trackerId] = browser;
                _pages[trackerId] = page;

                _logger.LogInformation($"Launched browser for account {account.Username} with tracker ID {trackerId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to launch browser for account {account.Username}");
                return false;
            }
        }

        private async Task<string> EnsureChromiumAsync()
        {
            if (!string.IsNullOrWhiteSpace(_cachedChromiumExecutablePath) && File.Exists(_cachedChromiumExecutablePath))
            {
                return _cachedChromiumExecutablePath;
            }

            await _chromiumDownloadLock.WaitAsync();
            try
            {
                if (!string.IsNullOrWhiteSpace(_cachedChromiumExecutablePath) && File.Exists(_cachedChromiumExecutablePath))
                {
                    return _cachedChromiumExecutablePath;
                }

                var appDataPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Chromium");
                Directory.CreateDirectory(appDataPath);

                var fetcher = new BrowserFetcher(new BrowserFetcherOptions
                {
                    Path = appDataPath
                });

                var revision = BrowserFetcher.DefaultChromiumRevision;
                var revisionInfo = fetcher.RevisionInfo(revision);
                if (!revisionInfo.Local)
                {
                    _logger.LogInformation("Downloading Chromium (revision {Revision})...", revision);

                    ChromiumDownloadWindow? progressWindow = null;

                    try
                    {
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            progressWindow = new ChromiumDownloadWindow
                            {
                                Owner = Application.Current.MainWindow,
                                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                                ShowInTaskbar = false
                            };
                            progressWindow.Show();
                        });

                        fetcher.DownloadProgressChanged += (sender, args) =>
                        {
                            try
                            {
                                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    if (progressWindow != null)
                                    {
                                        var percent = args.ProgressPercentage;
                                        progressWindow.UpdateProgress(percent, $"Downloading Chromium... {percent:0}%");
                                    }
                                }));
                            }
                            catch
                            {
                                // Ignore UI update failures
                            }
                        };

                        await fetcher.DownloadAsync(revision);

                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            if (progressWindow != null)
                            {
                                progressWindow.MarkCompleted();
                                progressWindow.Close();
                            }
                        });
                    }
                    finally
                    {
                        if (progressWindow != null)
                        {
                            try
                            {
                                Application.Current?.Dispatcher.Invoke(() =>
                                {
                                    if (progressWindow.IsVisible)
                                    {
                                        progressWindow.Close();
                                    }
                                });
                            }
                            catch
                            {
                                // Ignore cleanup errors
                            }
                        }
                    }
                }

                _cachedChromiumExecutablePath = revisionInfo.ExecutablePath;
                return _cachedChromiumExecutablePath;
            }
            finally
            {
                _chromiumDownloadLock.Release();
            }
        }

        public async Task<bool> CloseBrowserAsync(Account account)
        {
            try
            {
                if (string.IsNullOrEmpty(account.BrowserTrackerId))
                {
                    return false;
                }

                var trackerId = account.BrowserTrackerId;

                if (_pages.TryGetValue(trackerId, out var page))
                {
                    await page.CloseAsync();
                    _pages.Remove(trackerId);
                }

                if (_browsers.TryGetValue(trackerId, out var browser))
                {
                    await browser.CloseAsync();
                    _browsers.Remove(trackerId);
                }

                account.BrowserTrackerId = string.Empty;

                // Clean up user data directory
                try
                {
                    var userDataDir = GetUserDataDirectory(account);
                    if (Directory.Exists(userDataDir))
                    {
                        Directory.Delete(userDataDir, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }

                _logger.LogInformation($"Closed browser for account {account.Username}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to close browser for account {account.Username}");
                return false;
            }
        }

        public async Task<bool> IsBrowserRunningAsync(Account account)
        {
            if (string.IsNullOrEmpty(account.BrowserTrackerId))
            {
                return false;
            }

            var trackerId = account.BrowserTrackerId;
            return _browsers.ContainsKey(trackerId) && _pages.ContainsKey(trackerId);
        }

        public async Task<string?> GetBrowserTrackerIdAsync(Account account)
        {
            return account.BrowserTrackerId;
        }

        public async Task SetBrowserTrackerIdAsync(Account account, string trackerId)
        {
            account.BrowserTrackerId = trackerId;
        }

        private string GetUserDataDirectory(Account account)
        {
            var tempPath = Path.GetTempPath();
            var browserDataPath = Path.Combine(tempPath, "BloxManager", "Browsers", account.Id);
            Directory.CreateDirectory(browserDataPath);
            return browserDataPath;
        }

        public async Task<bool> ApproveQuickLoginAsync(Account account, string code)
        {
            try
            {
                var chromiumExecutablePath = await EnsureChromiumAsync();
                
                var browserOptions = new LaunchOptions
                {
                    Headless = true,
                    ExecutablePath = chromiumExecutablePath,
                    Args = new[] 
                    { 
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-blink-features=AutomationControlled" 
                    }
                };

                using var browser = await Puppeteer.LaunchAsync(browserOptions);
                using var page = await browser.NewPageAsync();

                // Set auth cookie
                await page.SetCookieAsync(new CookieParam
                {
                    Name = ".ROBLOSECURITY",
                    Value = account.SecurityToken,
                    Domain = ".roblox.com"
                });

                // Navigate to the cross-device login page
                await page.GoToAsync("https://www.roblox.com/crossdevicelogin/DeviceAuth", new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }
                });
                
                // Wait for the code input boxes to render
                await Task.Delay(2000);
                
                // Roblox auto-focuses the first input box, so we can just type the 6 characters
                await page.Keyboard.TypeAsync(code);
                
                // Wait a moment for the UI to register the input, then click the primary Enter button
                await Task.Delay(1000);
                await page.EvaluateFunctionAsync(@"() => {
                    const btns = Array.from(document.querySelectorAll('button'));
                    const enterBtn = btns.find(b => b.textContent.includes('Enter'));
                    if (enterBtn) enterBtn.click();
                }");
                
                // Wait for the confirmation screen to load
                await Task.Delay(2000);
                
                // Click the Confirm Login button
                await page.EvaluateFunctionAsync(@"() => {
                    const btns = Array.from(document.querySelectorAll('button'));
                    const confirmBtn = btns.find(b => b.textContent.includes('Confirm Login') || b.textContent.includes('Done'));
                    if (confirmBtn) confirmBtn.click();
                }");
                
                // Allow final request to settle
                await Task.Delay(2000);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Puppeteer quick login failed.");
                return false;
            }
        }

        public async Task<string?> LoginAndGetCookieAsync(string username, string password, CancellationToken cancellationToken = default)
            => await LoginAndGetCookieAsync(username, password, cancellationToken, false);

        public async Task<string?> LoginAndGetCookieAsync(string username, string password, CancellationToken cancellationToken, bool closeOnlyOnSuccess)
        {
            IBrowser? browser = null;
            IPage? page = null;
            string? resultSec = null;
            try
            {
                var chromiumExecutablePath = await EnsureChromiumAsync();

                var userDataDir = Path.Combine(
                    Path.GetTempPath(), "BloxManager", "BulkLogin", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(userDataDir);

                var browserOptions = new LaunchOptions
                {
                    Headless = false,
                    DefaultViewport = null,
                    ExecutablePath = chromiumExecutablePath,
                    Args = new[]
                    {
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--no-first-run",
                        $"--user-data-dir={userDataDir}",
                        "--disable-blink-features=AutomationControlled"
                    }
                };
                browser = await Puppeteer.LaunchAsync(browserOptions);
                page = await browser.NewPageAsync();

                await page.GoToAsync("https://www.roblox.com/login", new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }
                });

                // Wait for the login form to be ready with resilient selectors
                try
                {
                    await page.WaitForSelectorAsync("#login-username, input#login-username, input[name=\"username\"], input[name=\"UserName\"]", new WaitForSelectorOptions { Timeout = 12000 });
                    await page.WaitForSelectorAsync("#login-password, input#login-password, input[type=\"password\"], input[name=\"password\"]", new WaitForSelectorOptions { Timeout = 12000 });
                    // Attempt to find a login button by several selectors; fallback to any button with 'Log In' text
                    var loginBtn = await page.QuerySelectorAsync("#login-button, button#login-button, button[type=\"submit\"], button[name=\"login\"]");
                    if (loginBtn == null)
                    {
                        await page.EvaluateFunctionAsync(@"() => {
                            const btns = Array.from(document.querySelectorAll('button,input[type=""submit""]'));
                            const b = btns.find(b => /log\s*in|sign\s*in/i.test(b.textContent||b.value||'')); 
                            if (b) { b.id = 'bm-login-btn'; }
                        }");
                        loginBtn = await page.QuerySelectorAsync("#bm-login-btn");
                    }
                    if (loginBtn == null)
                    {
                        throw new InvalidOperationException("Could not locate login button");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Login form elements not found for {Username}. Roblox may have changed their login page.", username);
                    // Keep the browser open so the user can manually log in
                    return null;
                }

                // Fill username and password using the same selectors as the old RAM
                if (!string.IsNullOrEmpty(username))
                {
                    await page.EvaluateFunctionAsync(@"(u) => {
                        const el = document.querySelector('#login-username, input#login-username, input[name=""username""], input[name=""UserName""]');
                        if (el) { el.focus(); el.value=''; el.dispatchEvent(new Event('input', {bubbles:true})); } 
                    }", username);
                    await page.TypeAsync("#login-username, input#login-username, input[name=\"username\"], input[name=\"UserName\"]", username);
                }
                if (!string.IsNullOrEmpty(password))
                {
                    await page.TypeAsync("#login-password, input#login-password, input[type=\"password\"], input[name=\"password\"]", password);
                }

                // Click the login button
                await page.ClickAsync("#login-button, #bm-login-btn, button[type=\"submit\"], button[name=\"login\"]");

                // Wait for navigation to complete and cookie to be available
                // Try multiple approaches to get the cookie
                string? sec = null;
                int attempts = 0;
                while (string.IsNullOrEmpty(sec) && attempts < 5)
                {
                    await Task.Delay(2000); // Wait 2 seconds between attempts
                    var cookies = await page.GetCookiesAsync("https://www.roblox.com", "https://roblox.com");
                    sec = cookies.FirstOrDefault(c => c.Name == ".ROBLOSECURITY")?.Value;
                    attempts++;
                    
                    if (!string.IsNullOrEmpty(sec))
                    {
                        _logger.LogInformation("Successfully logged in {Username} after {Attempts} attempts", username, attempts);
                        break;
                    }
                }

                if (string.IsNullOrEmpty(sec))
                {
                    _logger.LogWarning("Login completed but no .ROBLOSECURITY cookie found for {Username} after 5 attempts", username);
                }

                resultSec = sec;
                return resultSec;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Browser login failed for {Username}", username);
                return null;
            }
            finally
            {
                if (closeOnlyOnSuccess)
                {
                    if (!string.IsNullOrEmpty(resultSec))
                    {
                        try { if (page != null) await page.CloseAsync(); } catch { }
                        try { if (browser != null) await browser.CloseAsync(); } catch { }
                    }
                    // else: keep browser open so user can resolve challenges or retry
                }
                else
                {
                    try { if (page != null) await page.CloseAsync(); } catch { }
                    try { if (browser != null) await browser.CloseAsync(); } catch { }
                }
            }
        }

        public async Task<bool> JoinGameViaWebAsync(Account account, long placeId, string? jobId = null, string? launchData = null, CancellationToken cancellationToken = default)
        {
            IBrowser? browser = null;
            IPage? page = null;
            try
            {
                var chromiumExecutablePath = await EnsureChromiumAsync();
                var userDataDir = GetUserDataDirectory(account);

                var browserOptions = new LaunchOptions
                {
                    Headless = false,
                    DefaultViewport = null,
                    ExecutablePath = chromiumExecutablePath,
                    Args = new[]
                    {
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--no-first-run",
                        $"--user-data-dir={userDataDir}",
                        "--disable-blink-features=AutomationControlled"
                    }
                };

                browser = await Puppeteer.LaunchAsync(browserOptions);
                page = await browser.NewPageAsync();

                // Set auth cookie
                if (!string.IsNullOrEmpty(account.SecurityToken))
                {
                    await page.SetCookieAsync(new CookieParam
                    {
                        Name = ".ROBLOSECURITY",
                        Value = account.SecurityToken,
                        Domain = ".roblox.com"
                    });
                }

                var url = $"https://www.roblox.com/games/{placeId}/";
                await page.GoToAsync(url, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle2 } });

                // Attempt to click the play button
                await page.EvaluateFunctionAsync(@"() => {
                    function clickPlay() {
                        const selectors = [
                            'button[data-testid=""play-button""]',
                            'button[aria-label*=""Play""]',
                            'button:contains(""Play"")',
                            'a[href*=""roblox-player""]'
                        ];
                        for (const sel of selectors) {
                            const el = document.querySelector(sel);
                            if (el) { el.click(); return true; }
                        }
                        // Fallback: search buttons by text
                        const btns = Array.from(document.querySelectorAll('button,a'));
                        const b = btns.find(b => /play|join/i.test(b.textContent||'')); 
                        if (b) { b.click(); return true; }
                        return false;
                    }
                    return clickPlay();
                }");

                await Task.Delay(1200);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed web join for account {Username}", account.Username);
                return false;
            }
            finally
            {
                try { if (page != null) await page.CloseAsync(); } catch { }
                try { if (browser != null) await browser.CloseAsync(); } catch { }
            }
        }

        public async Task CleanupAllBrowsersAsync()
        {
            try
            {
                var tasks = _browsers.Values.Select(async browser =>
                {
                    try
                    {
                        await browser.CloseAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to close browser during cleanup");
                    }
                });

                await Task.WhenAll(tasks);

                _browsers.Clear();
                _pages.Clear();

                // Clean up temp directories
                var tempPath = Path.Combine(Path.GetTempPath(), "BloxManager", "Browsers");
                if (Directory.Exists(tempPath))
                {
                    try
                    {
                        Directory.Delete(tempPath, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to cleanup browser temp directories");
                    }
                }

                _logger.LogInformation("Cleaned up all browsers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup browsers");
            }
        }
    }
}
