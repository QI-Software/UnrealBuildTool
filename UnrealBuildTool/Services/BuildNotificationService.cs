using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Serilog.Core;
using UnrealBuildTool.Build;

namespace UnrealBuildTool.Services
{
    public class BuildNotificationService
    {
        public static readonly string LogCategory = "BuildNotifier: ";
        
        private readonly ConfigurationService _config;
        private readonly DiscordClient _client;
        private readonly EmbedService _embed;
        private readonly Logger _log;

        private AutomatedBuild _currentBuild;
        private DiscordChannel _buildOutputChannel;
        private DiscordMessage _buildStatusMessage;
        private DiscordMessage _buildOutputMessage;

        private List<string> _currentOutput;

        public BuildNotificationService(ConfigurationService config, DiscordClient client, EmbedService embed, Logger log)
        {
            _config = config;
            _client = client;
            _embed = embed;
            _log = log;
            
            _client.Ready += OnReady;
        }

        public bool IsLinkedToChannel()
        {
            return _buildOutputChannel != null;
        }

        public async Task InitializeBuildNotifications(AutomatedBuild build)
        {
            if (_buildOutputChannel == null)
            {
                throw new NullReferenceException($"Build output channel is null.");
            }

            _currentBuild = build;

            _currentOutput = new List<string>()
            {
                "== UnrealBuildTool Live Console =="
            };
            
            _buildOutputMessage = await _buildOutputChannel.SendMessageAsync(Formatter.BlockCode("== UnrealBuildTool Live Console =="));

            var embed = _embed.LiveBuildStatus(build);

            _buildStatusMessage = await _buildOutputChannel.SendMessageAsync(embed);
        }

        public async Task UpdateBuildStateAsync()
        {
            await _buildStatusMessage.ModifyAsync(_embed.LiveBuildStatus(_currentBuild));
        }

        public async Task OnBuildCompletedAsync()
        {
            await _buildStatusMessage.ModifyAsync(_embed.LiveBuildStatus(_currentBuild));
            _currentBuild = null;
            _buildStatusMessage = null;
            _buildOutputMessage = null;
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