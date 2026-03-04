using Newtonsoft.Json;
using System;

namespace BloxManager.Models
{
    public class Game
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("creator")]
        public Creator Creator { get; set; } = new();

        [JsonProperty("rootPlace")]
        public Place RootPlace { get; set; } = new();

        [JsonProperty("price")]
        public int Price { get; set; }

        [JsonProperty("allowedGearGenres")]
        public string[] AllowedGearGenres { get; set; } = Array.Empty<string>();

        [JsonProperty("allowedGearCategories")]
        public string[] AllowedGearCategories { get; set; } = Array.Empty<string>();

        [JsonProperty("isGenreEnforced")]
        public bool IsGenreEnforced { get; set; }

        [JsonProperty("genre")]
        public string Genre { get; set; } = string.Empty;

        [JsonProperty("isAllGenre")]
        public bool IsAllGenre { get; set; }

        [JsonProperty("isFavoritedByUser")]
        public bool IsFavoritedByUser { get; set; }

        [JsonProperty("favoritedCount")]
        public int FavoritedCount { get; set; }

        [JsonProperty("onlineCount")]
        public int OnlineCount { get; set; }

        [JsonProperty("created")]
        public DateTime Created { get; set; }

        [JsonProperty("updated")]
        public DateTime Updated { get; set; }

        [JsonProperty("playAccess")]
        public string PlayAccess { get; set; } = string.Empty;

        [JsonProperty("isArchived")]
        public bool IsArchived { get; set; }

        [JsonProperty("builder")]
        public string Builder { get; set; } = string.Empty;

        [JsonProperty("builderId")]
        public long BuilderId { get; set; }

        [JsonProperty("isPlayable")]
        public bool IsPlayable { get; set; }

        [JsonProperty("providesSuperSafeChat")]
        public bool ProvidesSuperSafeChat { get; set; }

        [JsonProperty("copyProtected")]
        public bool CopyProtected { get; set; }
    }

    public class Creator
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("isRNVAccount")]
        public bool IsRNVAccount { get; set; }

        [JsonProperty("hasVerifiedBadge")]
        public bool HasVerifiedBadge { get; set; }
    }

    public class Place
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("url")]
        public string Url { get; set; } = string.Empty;

        [JsonProperty("isPlayable")]
        public bool IsPlayable { get; set; }

        [JsonProperty("reasonProhibited")]
        public string ReasonProhibited { get; set; } = string.Empty;

        [JsonProperty("price")]
        public int Price { get; set; }

        [JsonProperty("imageToken")]
        public string ImageToken { get; set; } = string.Empty;

        [JsonProperty("builder")]
        public string Builder { get; set; } = string.Empty;

        [JsonProperty("builderId")]
        public long BuilderId { get; set; }
    }

    public class Server
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("maxPlayers")]
        public int MaxPlayers { get; set; }

        [JsonProperty("playing")]
        public int Playing { get; set; }

        [JsonProperty("playerCount")]
        public int PlayerCount { get; set; }

        [JsonProperty("ping")]
        public int Ping { get; set; }

        [JsonProperty("fps")]
        public float Fps { get; set; }

        [JsonProperty("showSlowGameMessage")]
        public bool ShowSlowGameMessage { get; set; }

        [JsonProperty("region")]
        public string Region { get; set; } = string.Empty;

        [JsonProperty("regionCode")]
        public string RegionCode { get; set; } = string.Empty;

        [JsonProperty("countryCode")]
        public string CountryCode { get; set; } = string.Empty;

        [JsonProperty("capacity")]
        public int Capacity { get; set; }

        [JsonProperty("showShutdownAllButton")]
        public bool ShowShutdownAllButton { get; set; }

        [JsonProperty("friendsDescription")]
        public string FriendsDescription { get; set; } = string.Empty;

        [JsonProperty("friendsMouseover")]
        public string FriendsMouseover { get; set; } = string.Empty;

        [JsonProperty("friendsJoinScript")]
        public string FriendsJoinScript { get; set; } = string.Empty;

        [JsonProperty("friendsIcon")]
        public string FriendsIcon { get; set; } = string.Empty;

        [JsonProperty("currentPlayers")]
        public Player[] CurrentPlayers { get; set; } = Array.Empty<Player>();

        [JsonProperty("serverType")]
        public string ServerType { get; set; } = string.Empty;

        [JsonProperty("uid")]
        public string Uid { get; set; } = string.Empty;

        [JsonProperty("siteId")]
        public long SiteId { get; set; }

        [JsonProperty("accessType")]
        public string AccessType { get; set; } = string.Empty;

        [JsonProperty("participants")]
        public Participant[] Participants { get; set; } = Array.Empty<Participant>();
    }

    public class Player
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("thumbnail")]
        public string Thumbnail { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;
    }

    public class Participant
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("thumbnail")]
        public string Thumbnail { get; set; } = string.Empty;

        [JsonProperty("hasVerifiedBadge")]
        public bool HasVerifiedBadge { get; set; }

        [JsonProperty("joinScript")]
        public string JoinScript { get; set; } = string.Empty;
    }
}
