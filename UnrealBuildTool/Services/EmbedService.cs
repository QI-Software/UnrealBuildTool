﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using UnrealBuildTool.Build;

namespace UnrealBuildTool.Services
{
    public class EmbedService
    {
        private ConfigurationService _config;
        private DiscordClient _client;
        
        public void InjectServices(IServiceProvider services)
        {
            _config = services.GetRequiredService<ConfigurationService>();
            _client = services.GetRequiredService<DiscordClient>();
        }
        
        public DiscordEmbed Message(string message, DiscordColor color)
        {
            var builder = new DiscordEmbedBuilder()
                .WithDescription(Formatter.Bold(message))
                .WithColor(color);

            return builder.Build();
        }

        public DiscordEmbed LiveBuildStatus(AutomatedBuild build)
        {
            var user = build.GetInstigator();

            var sb = new StringBuilder();
            var backgroundSb = new StringBuilder();
            
            var stages = build.GetStages();
            
            foreach (var stage in stages)
            {
                var emote = "⏳";

                if (build.IsCompleted())
                {
                    emote = "✅";
                }
                else
                {
                    switch (stage.StageResult)
                    {
                        case StageResult.Scheduled:
                            emote = "⏳";
                            break;
                        case StageResult.Successful:
                            emote = "✅";
                            break;
                        case StageResult.SuccessfulWithWarnings:
                            emote = "⚠";
                            break;
                        case StageResult.Failed:
                            emote = "⛔";
                            break;
                        case StageResult.Running when !stage.RunInBackground():
                            emote = DiscordEmoji.FromGuildEmote(_client, _config.Discord.LoadingEmoteId.GetValueOrDefault()).ToString();
                            break;
                        case StageResult.Running when stage.RunInBackground():
                            emote = "💤";
                            break;
                        default:
                            emote = "?";
                            break;
                    }
                }
                
                sb.AppendLine($"{emote} {stage.GetDescription()}");
                if (stage.RunInBackground() && stage.StageResult != StageResult.Scheduled)
                {
                    switch (stage.StageResult)
                    {
                        case StageResult.Scheduled:
                            emote = "⏳";
                            break;
                        case StageResult.Successful:
                            emote = "✅";
                            break;
                        case StageResult.SuccessfulWithWarnings:
                            emote = "⚠";
                            break;
                        case StageResult.Failed:
                            emote = "⛔";
                            break;
                        case StageResult.Running:
                            emote = DiscordEmoji.FromGuildEmote(_client, _config.Discord.LoadingEmoteId.GetValueOrDefault()).ToString();
                            break;
                        default:
                            emote = "?";
                            break;
                    }
                    backgroundSb.AppendLine($"{emote} {stage.GetDescription()}");
                }
            }

            var embed = new DiscordEmbedBuilder()
                .WithAuthor($"{user.Username}#{user.Discriminator}", null, user.GetAvatarUrl(ImageFormat.Auto))
                .WithTitle("UnrealBuildTool - " + build.GetConfiguration().Name);

            if (build.IsCompleted())
            {
                embed.WithDescription($"Build completed successfully! Duration: {(DateTimeOffset.Now - build.GetStartTime()):c}")
                    .WithColor(DiscordColor.Green)
                    .WithTimestamp(build.GetStartTime());
            }
            else if (build.IsCancelled())
            {
                embed.WithDescription("Build cancelled.")
                    .WithColor(DiscordColor.Red)
                    .WithTimestamp(build.GetStartTime());
            }
            else if (build.IsFailed())
            {
                embed.WithDescription("Build failed.")
                    .WithColor(DiscordColor.Red)
                    .WithTimestamp(build.GetStartTime());
            }
            else if (build.IsStarted())
            {
                embed.WithDescription("Building...")
                    .WithColor(DiscordColor.Orange)
                    .WithTimestamp(build.GetStartTime());
            }
            else
            {
                embed.WithDescription("Starting build...")
                    .WithColor(DiscordColor.Blurple);
            }

            embed.AddField("**__Stages__**", sb.ToString());
            if (backgroundSb.Length != 0)
            {
                embed.AddField("**__Background Stages__**", backgroundSb.ToString());
            }

            if (build.IsFailed() && !build.IsCancelled())
            {
                var failedStage = build.GetStages().FirstOrDefault(s => s.StageResult == StageResult.Failed);

                if (failedStage != null)
                {
                    embed.AddField("**__Failure Reason__**", failedStage.FailureReason ?? "None specified.");
                }
            }
            
            return embed.Build();
        }
    }
}