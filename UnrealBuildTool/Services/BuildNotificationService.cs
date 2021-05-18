using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
            
            await _client.UpdateStatusAsync(new DiscordActivity("Building...", ActivityType.Playing),
                UserStatus.DoNotDisturb);
        }

        public void AddOutputData(string data)
        {
            _currentOutput.Add(data);
        }

        public void OnBuildCompleted()
        {
            _backgroundOutputSource.Cancel();
            _backgroundOutputTask = null;
        }

        public void OnBuildCancelled()
        {
            _backgroundOutputSource.Cancel();
            _backgroundOutputTask = null;
        }

        public void OnBuildFailed()
        {
            if (_currentBuild == null)
            {
                return;
            }
            
            _backgroundOutputSource.Cancel();
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
                try
                {
                    ArrayList Sync = ArrayList.Synchronized(_currentOutput);
                    var sb = new StringBuilder();

                    foreach (var obj in Sync)
                    {
                        if (obj is string s)
                        {
                            sb.AppendLine(s);
                        }
                    }
                
                    while (sb.ToString().Length > 1990)
                    {
                        Sync.RemoveAt(0);
                        sb = new StringBuilder();
                        foreach (var obj in Sync)
                        {
                            if (obj is string s)
                            {
                                sb.AppendLine(s);
                            }
                        }
                    }

                    var content = Formatter.BlockCode(sb.ToString());
                    var embed = _embed.LiveBuildStatus(_currentBuild);
                
                    if (content != _buildOutputMessage.Content || _buildOutputMessage.Embeds[0] != embed)
                    {
                        try
                        {
                            await _buildOutputMessage.ModifyAsync(content, embed);
                        }
                        catch (Exception e)
                        {
                            _log.Error(LogCategory + $"Failed to update build output message: {e.Message}");
                        }
                    }

                    if (lastUpdate)
                    {
                        break;
                    }

                    await Task.Delay(1000);
                    lastUpdate = _backgroundOutputSource.IsCancellationRequested;
                }
                catch (Exception ex)
                {
                    _log.Error(LogCategory + "An error has occured while handling console output. This is non fatal: " + ex.Message);
                }
            }
            
            _log.Information(LogCategory + "Stopped console output handler.");

            if (_currentBuild?.GetInstigator().IsBot is false)
            {
                await _buildOutputChannel.SendMessageAsync(
                    $"{_currentBuild?.GetInstigator().Mention} the build requires your attention.");
            }

            _currentBuild = null;
            _buildOutputMessage = null;
            _currentOutput = new ArrayList();
            
            await _client.UpdateStatusAsync(new DiscordActivity("Ready to build!", ActivityType.Playing),
                UserStatus.Online);
        }
        public async Task SendStageLogAsync(string stageName, StringBuilder log)
        {
            if (_buildOutputChannel == null)
            {
                return;
            }

            using MemoryStream memoryStream = new MemoryStream();
            using StreamWriter writer = new StreamWriter(memoryStream, Encoding.UTF8);
            foreach (var chunk in log.GetChunks())
            {
                await writer.WriteAsync(chunk);
            }

            await writer.FlushAsync();

            var msg = new DiscordMessageBuilder()
                .WithContent($"Output Log for Stage '{stageName}'")
                .WithFile("output.log", writer.BaseStream);

            await msg.SendAsync(_buildOutputChannel);
        }
    }
}