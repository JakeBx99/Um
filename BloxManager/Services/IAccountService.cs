using BloxManager.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BloxManager.Services
{
    public interface IAccountService
    {
        Task<List<Account>> GetAccountsAsync();
        Task<Account?> GetAccountAsync(string id);
        Task AddAccountAsync(Account account);
        Task UpdateAccountAsync(Account account);
        Task DeleteAccountAsync(string id);
        Task<bool> ValidateAccountAsync(Account account);
        Task<Account?> LoginAsync(string username, string password, bool keepBrowserOpenUntilSuccess = false);
        Task<Account?> LoginWithCookieAsync(string cookie);
        Task LogoutAsync(Account account);
        Task RefreshAccountAsync(Account account);
        Task<List<Account>> GetAccountsByGroupAsync(string group);
        Task<List<string>> GetGroupsAsync();
        Task SaveAccountsAsync();
        Task LoadAccountsAsync();
        Task<string> ExportAccountsAsync(List<Account> accounts);
        Task<List<Account>> ImportAccountsAsync(string data);
    }
}
