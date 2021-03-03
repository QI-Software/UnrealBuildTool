using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using UnrealBuildTool.Preconditions;
using UnrealBuildTool.Services;

namespace UnrealBuildTool.Commands
{
    [RequireBuildPermission]
    public class BuildCommands : BaseCommandModule
    {
        private readonly EmbedService _embed;

        public BuildCommands(EmbedService embed)
        {
            _embed = embed;
        }
        
        [Command("status")]
        public async Task GetStatus(CommandContext ctx)
        {
            await ctx.RespondAsync(_embed.Message($"There is no build in progress.", DiscordColor.Green));
        }
    }
}