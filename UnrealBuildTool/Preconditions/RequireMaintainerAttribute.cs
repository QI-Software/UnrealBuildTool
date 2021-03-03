using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using UnrealBuildTool.Services;

namespace UnrealBuildTool.Preconditions
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireMaintainerAttribute : CheckBaseAttribute
    {
        public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            var user = ctx.User;
            if (ctx.Client.CurrentApplication.Owners.Select(o => o.Id).Contains(user.Id))
            {
                return true;
            }

            var embed = ctx.Services.GetRequiredService<EmbedService>();
            var log = ctx.Services.GetRequiredService<Logger>();
            
            log.Warning($"User {ctx.User.Username}#{ctx.User.Discriminator} failed precondition {typeof(RequireMaintainerAttribute)}");
            await ctx.RespondAsync(embed.Message("This command is locked to maintainers only.", DiscordColor.Red));
            return false;
        }
    }
}