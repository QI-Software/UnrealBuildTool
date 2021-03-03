using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using UnrealBuildTool.Services;

namespace UnrealBuildTool.Preconditions
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireBuildPermissionAttribute : CheckBaseAttribute
    {
        public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            var config = ctx.Services.GetRequiredService<ConfigurationService>();
            var embed = ctx.Services.GetRequiredService<EmbedService>();
            
            if (!ctx.Guild.Roles.ContainsKey(config.Discord.BuildRoleId ?? 0))
            {
                await ctx.RespondAsync(
                    embed.Message($"Error: this server does not have the Build CI role with ID '{config.Discord.BuildRoleId}', please set it in the configuration file.",
                    DiscordColor.Red));
                return false;
            }

            if (ctx.Member.Roles.Any(r => r.Id == config.Discord.BuildRoleId))
            {
                return true;
            }

            await ctx.RespondAsync(embed.Message("Error: you do not have permission to use this command.", DiscordColor.Red));
            return false;
        }
    }
}