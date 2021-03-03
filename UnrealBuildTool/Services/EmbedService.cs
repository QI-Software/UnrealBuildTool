using System;
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
            var stages = build.GetStages();

            for (int i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
                var emote = "⏳";

                if (build.IsCompleted())
                {
                    emote = "✅";
                }
                else if (i == build.GetCurrentStageIndex())
                {
                    emote = DiscordEmoji.FromGuildEmote(_client, _config.Discord.LoadingEmoteId.GetValueOrDefault()).ToString();
                }
                else if (stage.StageResult == StageResult.Successful)
                {
                    emote = "✅";
                }
                else if (stage.StageResult == StageResult.SuccessfulWithWarnings)
                {
                    emote = "🚩";
                }
                sb.AppendLine($"{emote} {stage.GetName()}");
            }

            var embed = new DiscordEmbedBuilder()
                .WithAuthor($"{user.Username}#{user.Discriminator}", null, user.GetAvatarUrl(ImageFormat.Auto))
                .WithTitle("UnrealBuildTool - " + build.GetConfiguration().Name);

            if (build.IsCompleted())
            {
                embed.WithDescription("Build completed successfully!")
                    .WithColor(DiscordColor.Green)
                    .WithTimestamp(build.GetStartTime());
            }
            else if (build.IsStarted())
            {
                embed.WithDescription("Building...")
                    .WithColor(DiscordColor.Orange)
                    .WithTimestamp(build.GetStartTime());
            }
            else // TODO: Failed.
            {
                embed.WithDescription("Starting build...")
                    .WithColor(DiscordColor.Blurple);
            }
            
            embed.AddField("**__Stages__**", sb.ToString()).Build();
            
            return embed;
        }
    }
}