using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using UnrealBuildTool.Preconditions;
using UnrealBuildTool.Services;

namespace UnrealBuildTool.Commands
{
    [Group("steam")]
    [RequireBuildPermission]
    public class SteamAuthCommands : BaseCommandModule
    {
        private readonly EmbedService _embedService;
        private readonly SteamAuthService _steamAuth;

        public SteamAuthCommands(EmbedService embed, SteamAuthService steamAuth)
        {
            _embedService = embed;
            _steamAuth = steamAuth;
        }

        [Command("add")]
        public async Task AddAccount(CommandContext ctx)
        {
            var interactivity = ctx.Client.GetInteractivity();
            await ctx.RespondAsync(_embedService.Message("Please check DMs for further instructions.", DiscordColor.Green));
        }
    }
}