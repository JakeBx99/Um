using System.Threading.Tasks;

namespace BloxManager.Services
{
    public interface ISettingsService
    {
        Task<T> GetSettingAsync<T>(string key, T defaultValue = default!);
        Task SetSettingAsync<T>(string key, T value);
        Task<bool> GetMultiRobloxEnabledAsync();
        Task SetMultiRobloxEnabledAsync(bool enabled);
        Task<bool> GetSavePasswordsEnabledAsync();
        Task SetSavePasswordsEnabledAsync(bool enabled);
        Task<string> GetThemeAsync();
        Task SetThemeAsync(string theme);
        Task<bool> GetDeveloperModeEnabledAsync();
        Task SetDeveloperModeEnabledAsync(bool enabled);
        Task<int> GetWebServerPortAsync();
        Task SetWebServerPortAsync(int port);
        Task<string> GetDefaultGroupAsync();
        Task SetDefaultGroupAsync(string group);
        Task<string> GetPlaceIdAsync();
        Task SetPlaceIdAsync(string placeId);
        Task<string> GetJobIdAsync();
        Task SetJobIdAsync(string jobId);
        Task<string> GetLaunchDataAsync();
        Task SetLaunchDataAsync(string launchData);
        Task<string?> GetLicenseUsernameAsync();
        Task SetLicenseUsernameAsync(string? username);
        Task<bool> IsLicenseVerifiedAsync();
        Task SetLicenseVerifiedAsync(bool verified);
        Task<System.Collections.Generic.List<string>> GetPersistentGroupsAsync();
        Task SetPersistentGroupsAsync(System.Collections.Generic.List<string> groups);
        Task<string> GetUpdateChannelAsync();
        Task SetUpdateChannelAsync(string channel);
        Task<bool> GetLowMemoryModeAsync();
        Task SetLowMemoryModeAsync(bool enabled);
    }
}
