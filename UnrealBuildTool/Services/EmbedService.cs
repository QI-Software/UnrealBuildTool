using System;
using System.Net.Http;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;

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
    }
}