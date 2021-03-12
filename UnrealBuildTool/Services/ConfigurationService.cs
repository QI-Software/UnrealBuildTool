using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnrealBuildTool.Models;

namespace UnrealBuildTool.Services
{
    public class ConfigurationService
    {
        [JsonProperty] 
        public DiscordConfig Discord { get; private set; } = new DiscordConfig();

        [JsonProperty] 
        public int BuildScheduleCheckIntervalMilliseconds { get; private set; } = 1000;

        public static bool Exists()
        {
            return File.Exists("config/config.json");
        }

        public static async Task<ConfigurationService> LoadConfigurationAsync()
        {
            if (Exists())
            {
                var json = await File.ReadAllTextAsync("config/config.json");
                var settings = new JsonSerializerSettings
                {
                    DefaultValueHandling = DefaultValueHandling.Populate,
                };
                
                var config = JsonConvert.DeserializeObject<ConfigurationService>(json, settings);
                return config;
            }

            var newConfig = new ConfigurationService();
            var newSettings = new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Include,
            };

            Directory.CreateDirectory("config");

            var newJson = JsonConvert.SerializeObject(newConfig, Formatting.Indented, newSettings);
            await File.WriteAllTextAsync("config/config.json", newJson);
            return newConfig;
        }

        public async Task SaveConfigurationAsync()
        {
            var settings = new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Include,
            };
            
            var json = JsonConvert.SerializeObject(this, Formatting.Indented, settings);
            Directory.CreateDirectory("config");
            await File.WriteAllTextAsync("config/config.json", json);
        }

        public bool IsConfigurationValid(out string ErrorMessage)
        {
            if (Discord == null)
            {
                ErrorMessage =
                    "Missing 'Discord' property in configuration, please regenerate your configuration file.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Discord.Prefix))
            {
                ErrorMessage = "Invalid Discord prefix set, please set a non-empty and non-whitespace prefix.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Discord.Token))
            {
                ErrorMessage = "Invalid Discord token set, please set a valid bot token.";
                return false;
            }

            if (Discord.BuildRoleId == null || Discord.BuildRoleId < 0)
            {
                ErrorMessage =
                    "No valid build role ID has been set, please create a build role and attribute it to users who can trigger new builds.";
                return false;
            }
            
            if (Discord.BuildChannelId == null || Discord.BuildChannelId < 0)
            {
                ErrorMessage =
                    "No valid build channel ID has been set, please create a build channel and set its ID in the config file.";
                return false;
            }
            
            if (Discord.LoadingEmoteId == null || Discord.LoadingEmoteId < 0)
            {
                ErrorMessage =
                    "No valid loadimg emote ID has been set, please add a loading emote and set its ID in the config file.";
                return false;
            }

            ErrorMessage = null;
            return true;
        }
    }
}