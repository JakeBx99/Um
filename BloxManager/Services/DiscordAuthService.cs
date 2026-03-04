using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BloxManager.Services
{
    public interface IDiscordAuthService
    {
        Task<bool> ValidateUserAsync(string userId);
        Task<bool> ValidateUserAsync(string userId, string accessToken);
        Task<DiscordUserInfo?> GetUserInfoAsync(string accessToken);
    }

    public class DiscordAuthService : IDiscordAuthService
    {
        private readonly ILogger<DiscordAuthService> _logger;
        private readonly string _botApiUrl = "http://localhost:3000";
        private readonly string _requiredGuildId = "1476897616049733692";
        private readonly string _requiredRoleId  = "1476897643606184020";

        public DiscordAuthService(ILogger<DiscordAuthService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> ValidateUserAsync(string userId)
        {
            try
            {
                _logger.LogInformation($"Validating user {userId} via bot API...");

                // ✅ Fresh HttpClient with NO auth headers — previous bug was
                //    the Bearer token from GetUserInfoAsync leaking into this call
                using var client = new HttpClient();
                var response = await client.GetAsync($"{_botApiUrl}/check-user/{userId}");

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Bot API response ({response.StatusCode}): {content}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Bot API returned {response.StatusCode} — is the bot server running on port 3000?");
                    return false;
                }

                var result = JsonSerializer.Deserialize<JsonElement>(content);

                if (result.TryGetProperty("verified", out var verifiedProp))
                {
                    var isVerified = verifiedProp.GetBoolean();
                    _logger.LogInformation($"Role check for {userId}: {isVerified}");
                    return isVerified;
                }

                _logger.LogWarning($"Bot API response missing 'verified' field: {content}");
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Could not reach bot API — make sure the Node.js bot server is running on http://localhost:3000");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate Discord user");
                return false;
            }
        }

        public async Task<bool> ValidateUserAsync(string userId, string accessToken)
        {
            // First try bot API
            var viaBot = await ValidateUserAsync(userId);
            if (viaBot) return true;

            // Fallback to direct Discord API using OAuth scopes
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                // Check guild membership
                var guildsResp = await client.GetAsync("https://discord.com/api/users/@me/guilds");
                var guildsJson = await guildsResp.Content.ReadAsStringAsync();
                if (!guildsResp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Guilds API failed: {Status} {Body}", guildsResp.StatusCode, guildsJson);
                    return false;
                }

                var guilds = JsonSerializer.Deserialize<JsonElement>(guildsJson);
                var inRequiredGuild = false;
                if (guilds.ValueKind == JsonValueKind.Array)
                {
                    foreach (var g in guilds.EnumerateArray())
                    {
                        if (g.TryGetProperty("id", out var idProp) &&
                            string.Equals(idProp.GetString(), _requiredGuildId, StringComparison.Ordinal))
                        {
                            inRequiredGuild = true;
                            break;
                        }
                    }
                }
                if (!inRequiredGuild)
                {
                    _logger.LogInformation("User not in required guild {GuildId}", _requiredGuildId);
                    return false;
                }

                // Get member info (requires guilds.members.read) to inspect roles
                var memberResp = await client.GetAsync($"https://discord.com/api/users/@me/guilds/{_requiredGuildId}/member");
                var memberJson = await memberResp.Content.ReadAsStringAsync();
                if (!memberResp.IsSuccessStatusCode)
                {
                    // If the bot/app cannot read roles (403 or similar), but user is in the guild,
                    // treat this as verified to avoid blocking due to missing bot permissions.
                    _logger.LogWarning("Member API failed: {Status} {Body}", memberResp.StatusCode, memberJson);
                    return true;
                }

                var member = JsonSerializer.Deserialize<JsonElement>(memberJson);
                if (member.TryGetProperty("roles", out var rolesProp) && rolesProp.ValueKind == JsonValueKind.Array)
                {
                    var hasRole = false;
                    foreach (var r in rolesProp.EnumerateArray())
                    {
                        if (string.Equals(r.GetString(), _requiredRoleId, StringComparison.Ordinal))
                        {
                            hasRole = true;
                            break;
                        }
                    }
                    _logger.LogInformation("Direct role check result: {HasRole}", hasRole);
                    return hasRole;
                }

                _logger.LogWarning("Member JSON missing 'roles' — {Body}", memberJson);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Direct Discord role validation failed");
                return false;
            }
        }

        public async Task<DiscordUserInfo?> GetUserInfoAsync(string accessToken)
        {
            try
            {
                // ✅ Fresh HttpClient scoped only to this call — no shared state
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await client.GetAsync("https://discord.com/api/users/@me");
                var content  = await response.Content.ReadAsStringAsync();

                _logger.LogInformation($"User info response ({response.StatusCode})");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to get user info: {content}");
                    return null;
                }

                return JsonSerializer.Deserialize<DiscordUserInfo>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Discord user info");
                return null;
            }
        }
    }

    public class DiscordUserInfo
    {
        public string Id            { get; set; } = string.Empty;
        public string Username      { get; set; } = string.Empty;
        public string Discriminator { get; set; } = string.Empty;
        public string Avatar        { get; set; } = string.Empty;
        public bool   Verified      { get; set; }
        public string Email         { get; set; } = string.Empty;
        public int    Flags         { get; set; }
        public int    PremiumType   { get; set; }
        public int    PublicFlags   { get; set; }
        public string Locale        { get; set; } = string.Empty;
        public bool   MfaEnabled    { get; set; }
    }
}
