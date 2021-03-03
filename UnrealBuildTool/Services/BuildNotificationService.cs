using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Serilog.Core;

namespace UnrealBuildTool.Services
{
    public class BuildNotificationService
    {
        public static readonly string LogCategory = "BuildNotifier: ";
        
        private readonly ConfigurationService _config;
        private readonly DiscordClient _client;
        private readonly Logger _log;

        private DiscordChannel _buildOutputChannel;
        private DiscordMessage _buildStatusMessage;
        private DiscordMessage _buildOutputMessage;

        public BuildNotificationService(ConfigurationService config, DiscordClient client, Logger log)
        {
            _config = config;
            _client = client;
            _log = log;
            
            _client.Ready += OnReady;
        }

        public bool IsLinkedToChannel()
        {
            return _buildOutputChannel != null;
        }

        private async Task OnReady(DiscordClient sender, ReadyEventArgs e)
        {
            try
            {
                _buildOutputChannel = await _client.GetChannelAsync(_config.Discord.BuildChannelId.GetValueOrDefault());
            }
            catch (Exception ex)
            {
                _log.Error(LogCategory + $"Could not find any build notification channel with id '{_config.Discord.BuildChannelId}': " + ex.Message);
            }
        }
    }
}