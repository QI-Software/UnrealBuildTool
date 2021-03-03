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
        private readonly BuildService _buildService;
        private readonly EmbedService _embed;

        public BuildCommands(BuildService buildService, EmbedService embed)
        {
            _buildService = buildService;
            _embed = embed;
        }
        
        [Command("status")]
        public async Task GetStatus(CommandContext ctx)
        {
            if (!_buildService.IsBuilding())
            {
                await ctx.RespondAsync(_embed.Message("There is no build in progress.", DiscordColor.Green));
                return;
            }
        }
    }
}