using System.ComponentModel;
using Newtonsoft.Json;

namespace UnrealBuildTool.Models
{
    public class DiscordConfig
    {
        [JsonProperty] 
        [DefaultValue("!!")] 
        public string Prefix { get; private set; } = "!!";
        
        [JsonProperty]
        [DefaultValue(null)]
        public string Token { get; private set; }
        
        [JsonProperty]
        [DefaultValue(null)]
        public ulong? BuildRoleId { get; private set; }
        
        [JsonProperty]
        [DefaultValue(null)]
        public ulong? BuildChannelId { get; private set; }
    }
}