using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Windows;
using BloxManager.Services;
using BloxManager.ViewModels;
using BloxManager.Views;
using BloxManager.Logging;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

namespace BloxManager
{
    public partial class App : Application
    {
        private IHost? _host;
        private static System.Threading.Mutex? _singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                bool createdNew;
                _singleInstanceMutex = new System.Threading.Mutex(initiallyOwned: true, "Global\\BloxManager_SingleInstance", out createdNew);
                if (!createdNew)
                {
                    MessageBox.Show("BloxManager is already running.", "BloxManager", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }
            }
            catch
            {
                // If mutex fails, continue but multiple instances may run.
            }
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Services
                    services.AddSingleton<IAccountService, AccountService>();
                    services.AddSingleton<IRobloxService, RobloxService>();
                    services.AddSingleton<ISettingsService, SettingsService>();
                    services.AddSingleton<IEncryptionService, EncryptionService>();
                    services.AddSingleton<IBrowserService, BrowserService>();
                    services.AddSingleton<IGameService, GameService>();
                    services.AddSingleton<IWebApiService, WebApiService>();
                    // Discord auth removed for public build
                    services.AddSingleton<IUpdateService, UpdateService>();
                    
                    // ViewModels
                    services.AddSingleton<MainViewModel>();
                    services.AddTransient<AccountViewModel>();
                    services.AddTransient<AddAccountViewModel>();
                    services.AddTransient<SettingsViewModel>();
                    services.AddTransient<BulkImportViewModel>();
                    
                    // Views
                    services.AddSingleton<MainWindow>();
                    services.AddTransient<AddAccountWindow>();
                    services.AddTransient<AccountDetailsWindow>();
                    services.AddTransient<BulkImportWindow>();

                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    // Disable console/debug providers to reduce memory and handle logs via file provider only
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    logging.AddProvider(new FileLoggerProvider(baseDir));
                })
                .Build();

            _host.Start();

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                var logger = _host.Services.GetService<ILogger<App>>();
                logger?.LogError(ex.ExceptionObject as Exception, "Unhandled exception");
            };
            this.DispatcherUnhandledException += (s, ex) =>
            {
                var logger = _host.Services.GetService<ILogger<App>>();
                logger?.LogError(ex.Exception, "Dispatcher unhandled exception");
                ex.Handled = true;
            };
            // Auto-update check (run in background after startup so UI isn't blocked)
            _ = Task.Run(async () =>
            {
                try
                {
                    var settingsService = _host.Services.GetRequiredService<ISettingsService>();
                    var updateService = _host.Services.GetRequiredService<IUpdateService>();
                    var autoUpdate = await settingsService.GetSettingAsync<bool>("CheckForUpdates");
                    var prompted = await settingsService.GetSettingAsync<bool>("AutoUpdatePrompted");
                    if (!autoUpdate && !prompted)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            var result = MessageBox.Show("Enable automatic updates? You can change this anytime in Settings.",
                                                         "BloxManager", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (result == MessageBoxResult.Yes)
                            {
                                settingsService.SetSettingAsync("CheckForUpdates", true).GetAwaiter().GetResult();
                                autoUpdate = true;
                            }
                        });
                        await settingsService.SetSettingAsync("AutoUpdatePrompted", true);
                    }

                    var owner = await settingsService.GetSettingAsync<string>("UpdateRepoOwner") ?? "JakeBx99";
                    var repo  = await settingsService.GetSettingAsync<string>("UpdateRepoName")  ?? "Um";
                    var upd   = await updateService.CheckForUpdateAsync(owner, repo);
                    if (!upd.hasUpdate || string.IsNullOrEmpty(upd.downloadUrl)) return;

                    var path = await updateService.DownloadLatestAsync(upd.downloadUrl);
                    if (string.IsNullOrEmpty(path)) return;

                    if (autoUpdate)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                                Shutdown();
                            }
                            catch { /* if launch fails, keep app running */ }
                        });
                    }
                    else
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            var res = MessageBox.Show($"Update {upd.latest} is available. Install now?",
                                                      "BloxManager", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (res == MessageBoxResult.Yes)
                            {
                                try
                                {
                                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                                    Shutdown();
                                }
                                catch { }
                            }
                        });
                    }
                }
                catch { }
            });

            // Pre-download Chromium in background
            _ = Task.Run(async () =>
            {
                try
                {
                    var browserService = _host.Services.GetRequiredService<IBrowserService>();
                    // Create a minimal account for pre-download
                    var tempAccount = new BloxManager.Models.Account 
                    { 
                        Id = Guid.NewGuid().ToString(),
                        Username = "temp",
                        Password = ""
                    };
                    // This will trigger EnsureChromiumAsync to download if needed
                    await browserService.GetBrowserTrackerIdAsync(tempAccount);
                }
                catch
                {
                    // Ignore pre-download errors; it will retry on-demand
                }
            });
            _ = Task.Run(async () =>
            {
                var settings = _host.Services.GetRequiredService<ISettingsService>();
                while (true)
                {
                    try
                    {
                        var lowMem = await settings.GetLowMemoryModeAsync();
                        if (lowMem)
                        {
                            TrimWorkingSet();
                        }
                    }
                    catch { }
                    await Task.Delay(30000);
                }
            });

            // Show main window
            try
            {
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                this.MainWindow = mainWindow;
                this.ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                var logger = _host.Services.GetService<ILogger<App>>();
                if (logger != null)
                {
                    logger.LogError(ex, "Failed to show main window.");
                }
                MessageBox.Show($"Failed to start application: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _host?.Dispose();
            base.OnExit(e);
        }

        public static T GetService<T>() where T : class
        {
            if (((App)Current)._host?.Services.GetService(typeof(T)) is T service)
            {
                return service;
            }
            throw new InvalidOperationException($"Service of type {typeof(T).Name} not found.");
        }

        [DllImport("psapi.dll")]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);
        private static void TrimWorkingSet()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            catch { }
            try
            {
                var handle = Process.GetCurrentProcess().Handle;
                EmptyWorkingSet(handle);
            }
            catch { }
        }

        private static string ComputeHardwareId()
        {
            var machineName = Environment.MachineName;
            var userName = Environment.UserName;
            var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            var hwidString = $"{machineName}_{userName}_{systemDrive}";
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(hwidString));
            return Convert.ToBase64String(hash).Replace("=", "").Replace("+", "").Replace("/", "").Substring(0, 16);
        }
    }
}
