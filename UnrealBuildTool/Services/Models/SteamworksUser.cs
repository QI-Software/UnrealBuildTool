using Newtonsoft.Json;
using SteamAuth;

namespace UnrealBuildTool.Services.Models
{
    public class SteamworksUser
    {
        [JsonProperty]
        public string Username { get; internal set; }
        
        [JsonProperty]
        public string Password { get; internal set; }
        
        [JsonProperty]
        public SteamGuardAccount SteamGuard { get; internal set; }
    }
}