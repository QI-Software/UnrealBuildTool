using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
        private DiscordMessage _buildOutputMessage;

        private ArrayList _currentOutput;
        private Task _backgroundOutputTask;
        private CancellationTokenSource _backgroundOutputSource;

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

            _currentOutput = new ArrayList()
            {
                "== UnrealBuildTool Live Console =="
            };
            
            var embed = _embed.LiveBuildStatus(build);
            _buildOutputMessage = await _buildOutputChannel.SendMessageAsync(Formatter.BlockCode("== UnrealBuildTool Live Console =="), embed);
            
            _backgroundOutputSource = new CancellationTokenSource();
            _backgroundOutputTask = Task.Run(async () => await HandleConsoleOutputAsync());
        }

        public void AddOutputData(string data)
        {
            _currentOutput.Add(data);
        }

        public async Task OnBuildCompletedAsync()
        {
            _backgroundOutputSource.Cancel();
            
            _currentBuild = null;
            _backgroundOutputTask = null;
        }

        public async Task OnBuildFailedAsync()
        {
            if (_currentBuild == null)
            {
                return;
            }
            
            _backgroundOutputSource.Cancel();
            
            _currentBuild = null;
            _backgroundOutputTask = null;
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

        private async Task HandleConsoleOutputAsync()
        {
            _log.Information(LogCategory + "Handling live console output from processes.");
            bool lastUpdate = false;
            while (_currentBuild != null && _buildOutputMessage != null)
            {
                int fullLength = 0;

                ArrayList Sync = ArrayList.Synchronized(_currentOutput);

                foreach (var obj in Sync)
                {
                    if (obj is string s)
                    {
                        fullLength += s.Length;
                    }
                }

                while (fullLength > 1950)
                {
                    fullLength = 0;
                    Sync.RemoveAt(_currentOutput.Count - 1);
                    foreach (var obj in Sync)
                    {
                        if (obj is string s)
                        {
                            fullLength += s.Length;
                        }
                    }
                }

                var sb = new StringBuilder();
                foreach (var s in Sync)
                {
                    if (s is string str)
                    {
                        sb.AppendLine(str);
                    }
                }

                var content = Formatter.BlockCode(sb.ToString());
                var embed = _embed.LiveBuildStatus(_currentBuild);
                
                if (content != _buildOutputMessage.Content || _buildOutputMessage.Embeds[0] != embed)
                {
                    await _buildOutputMessage.ModifyAsync(content, embed);
                }

                if (lastUpdate)
                {
                    break;
                }

                try
                {
                    await Task.Delay(1000, _backgroundOutputSource.Token); // Update every 1.5 seconds.
                }
                catch (TaskCanceledException)
                {
                    lastUpdate = true;
                }
            }
            
            _log.Information(LogCategory + "Stopped console output handler.");
            _buildOutputMessage = null;
        }
    }
}