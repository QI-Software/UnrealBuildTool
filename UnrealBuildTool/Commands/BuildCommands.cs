using System;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
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

        [Command("listconfigs")]
        public async Task ListBuildConfigurations(CommandContext ctx)
        {
            var configs = _buildService.GetBuildConfigurations();

            if (configs.Count == 0)
            {
                await ctx.RespondAsync(_embed.Message("There are no build configurations available.", DiscordColor.Red));
                return;
            }
        }

        [Command("reloadconfigs")]
        public async Task ReloadBuildConfigurations(CommandContext ctx)
        {
            await _buildService.LoadBuildConfigurationsAsync();

            if (_buildService.IsBuilding())
            {
                await ctx.RespondAsync(_embed.Message("Cannot reload build configurations during a build.",
                    DiscordColor.Red));
                return;
            }

            var count = _buildService.GetBuildConfigurations().Count;
            if (count == 0)
            {
                await ctx.RespondAsync(_embed.Message("There are no configurations to reload.", DiscordColor.Red));
            }
            else
            {
                await ctx.RespondAsync(_embed.Message($"Reloaded {count} configurations.", DiscordColor.Green));
            }
        }

        [Command("startbuild")]
        [Aliases("build")]
        public async Task StartBuild(CommandContext ctx)
        {
            if (_buildService.IsBuilding())
            {
                await ctx.RespondAsync(_embed.Message("Cannot start multiple builds at the same time.",
                    DiscordColor.Red));
                return;
            }

            var configs = _buildService.GetBuildConfigurations();
            if (configs.Count == 0)
            {
                await ctx.RespondAsync(_embed.Message("Cannot start a build: no available build configurations.",
                    DiscordColor.Red));
                return;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < configs.Count; i++)
            {
                sb.AppendLine($"{i + 1} - {configs[i].Name}");
            }
            
            var embed = new DiscordEmbedBuilder()
                .WithTitle("UnrealBuildTool - Start Build")
                .WithDescription("Please type the number of the build configuration to run.")
                .WithColor(DiscordColor.Blurple)
                .AddField("**Available Configurations**", sb.ToString())
                .Build();

            await ctx.RespondAsync(embed);

            var interactivity = ctx.Client.GetInteractivity();
            
            var result =
                await interactivity.WaitForMessageAsync(m => m.Author.Id == ctx.User.Id, TimeSpan.FromMinutes(1));

            if (result.TimedOut)
            {
                await ctx.RespondAsync(_embed.Message("Timed out, please try again.", DiscordColor.Red));
                return;
            }

            int selection;
            if (!int.TryParse(result.Result.Content, out selection))
            {
                await ctx.RespondAsync(_embed.Message("Invalid number specified, please try again.", DiscordColor.Red));
                return;
            }

            selection--;
            if (selection < 0 || selection >= configs.Count)
            {
                await ctx.RespondAsync(_embed.Message("Please specify a valid number.", DiscordColor.Red));
                return;
            }
            
            var config = configs[selection];
            var description = config.Description;

            if (string.IsNullOrWhiteSpace(description))
            {
                description = "No description set.";
            }

            var warning = $@"You are about to start a build with the **{config.Name}** configuration.

                             Press ✅ to start the build.

                             Press 🛑 to cancel.";


            embed = new DiscordEmbedBuilder()
                .WithTitle("UnrealBuildTool - " + config.Name)
                .WithDescription(description)
                .WithColor(DiscordColor.Orange)
                .AddField("**Warning**", warning)
                .WithFooter("Warning - are you sure you want to start this build?")
                .Build();

            var msg = await ctx.RespondAsync(embed);
            await msg.CreateReactionAsync(DiscordEmoji.FromUnicode("✅"));
            _ = msg.CreateReactionAsync(DiscordEmoji.FromUnicode("🛑"));

            var reactResult = await interactivity.WaitForReactionAsync(m =>
            {
                return m.Message.Id == msg.Id
                       && m.User.Id == ctx.User.Id
                       && (m.Emoji == DiscordEmoji.FromUnicode("✅") || m.Emoji == DiscordEmoji.FromUnicode("🛑"));
            }, TimeSpan.FromMinutes(1));

            if (reactResult.TimedOut || reactResult.Result.Emoji == DiscordEmoji.FromUnicode("🛑"))
            {
                await ctx.RespondAsync(_embed.Message("Aborting build.", DiscordColor.Red));
                return;
            }

            var startStatus = await ctx.RespondAsync(_embed.Message("Starting build...", DiscordColor.Blurple));

            if (!_buildService.StartBuild(config, out string ErrorMessage))
            {
                await startStatus.ModifyAsync(
                    _embed.Message($"An error occured while starting the build: {ErrorMessage}", DiscordColor.Red));
                return;
            }

            await startStatus.ModifyAsync(_embed.Message("Successfully started build.", DiscordColor.Green));
        }
    }
}