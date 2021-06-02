using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using Newtonsoft.Json;
using UnrealBuildTool.Build;
using UnrealBuildTool.Preconditions;
using UnrealBuildTool.Services;

namespace UnrealBuildTool.Commands
{
    [RequireBuildPermission]
    public class BuildCommands : BaseCommandModule
    {
        private readonly BuildService _buildService;
        private readonly EmbedService _embed;
        private readonly HttpClient _httpClient;

        public BuildCommands(BuildService buildService, EmbedService embed, HttpClient httpClient)
        {
            _buildService = buildService;
            _embed = embed;
            _httpClient = httpClient;
        }

        [Command("status")]
        public async Task GetStatus(CommandContext ctx)
        {
            if (!_buildService.IsBuilding())
            {
                await ctx.RespondAsync(_embed.Message("There is no build in progress.", DiscordColor.Green));
                return;
            }

            var build = _buildService.GetCurrentBuild();
            var seconds = Math.Floor((DateTimeOffset.Now - build.GetStartTime()).TotalSeconds);

            await ctx.RespondAsync(_embed.Message(
                $"Currently running {build.GetConfiguration().Name}. Time elapsed: {seconds}s", DiscordColor.Green));
        }

        [Command("listconfigs")]
        public async Task ListBuildConfigurations(CommandContext ctx)
        {
            var configs = _buildService.GetBuildConfigurations();

            if (configs.Count == 0)
            {
                await ctx.RespondAsync(_embed.Message("There are no build configurations available.",
                    DiscordColor.Red));
                return;
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle("**__Available Configurations__**")
                .WithColor(DiscordColor.Blurple);

            foreach (var config in configs)
            {
                embed.AddField($"**{config.Name}**", config.Description);
            }

            await ctx.RespondAsync(embed.Build());
        }

        [Command("configinfo")]
        [Aliases("info")]
        public async Task ConfigInfo(CommandContext ctx, [RemainingText] string name)
        {
            var configs = _buildService.GetBuildConfigurations();

            var config = configs.FirstOrDefault(c => c.Name.ToLower() == name.ToLower());

            if (config == null)
            {
                await ctx.RespondAsync(_embed.Message($"Could not find any configuration with name '{name}'",
                    DiscordColor.Red));
                return;
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"**__{config.Name}__**")
                .WithDescription(config.Description)
                .WithColor(DiscordColor.Blurple);

            foreach (var stage in config.Stages)
            {
                if (!_buildService.StageExists(stage.Name))
                {
                    continue;
                }

                var instancedStage = _buildService.InstantiateStage(stage.Name);
                if (instancedStage == null)
                {
                    continue;
                }
                
                instancedStage.GenerateDefaultStageConfiguration();
                instancedStage.SetStageConfiguration(stage.Configuration);
                if (!instancedStage.IsStageConfigurationValid(out _))
                {
                    continue;
                }

                embed.AddField($"**__{instancedStage.GetName()}__**", instancedStage.GetDescription());
            }

            await ctx.RespondAsync(embed.Build());
        }

        [Command("reloadconfigs")]
        [Aliases("reload", "reloadconfig")]
        public async Task ReloadBuildConfigurations(CommandContext ctx)
        {
            if (_buildService.IsBuilding())
            {
                await ctx.RespondAsync(_embed.Message("Cannot reload build configurations during a build.",
                    DiscordColor.Red));
                return;
            }

            await _buildService.LoadBuildConfigurationsAsync();

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

                             Press **Start** to begin.

                             Press **Cancel** to abort.";
            
            var confirmButton = new DiscordButtonComponent(ButtonStyle.Danger, "confirm_build", "Start");
            var deleteButton = new DiscordButtonComponent(ButtonStyle.Primary, "cancel_build", "Cancel");

            embed = new DiscordEmbedBuilder()
                .WithTitle("UnrealBuildTool - " + config.Name)
                .WithDescription(description)
                .WithColor(DiscordColor.Orange)
                .AddField("**Warning**", warning)
                .WithFooter("Warning - are you sure you want to start this build?")
                .Build();

            var builder = new DiscordMessageBuilder()
                .WithEmbed(embed)
                .WithComponents(confirmButton, deleteButton);

            var msg = await builder.SendAsync(ctx.Channel);

            var buttonResult = await msg.WaitForButtonAsync(TimeSpan.FromMinutes(5));
            await msg.ModifyAsync(new DiscordMessageBuilder().WithEmbed(embed));
            if (buttonResult.TimedOut || buttonResult.Result.Id == "cancel_build")
            {
                await ctx.RespondAsync(_embed.Message("Aborting build.", DiscordColor.Red));
                return;
            }

            var startStatus = await ctx.RespondAsync(_embed.Message("Starting build...", DiscordColor.Orange));
            if (!_buildService.StartBuild(config, ctx.User, out string errorMessage))
            {
                await startStatus.ModifyAsync(
                    _embed.Message($"An error occured while starting the build: {errorMessage}", DiscordColor.Red));
                return;
            }

            await startStatus.ModifyAsync(_embed.Message("Successfully started build.", DiscordColor.Green));
        }

        [Command("forcebuild")]
        public async Task ForceStartBuild(CommandContext ctx, int number)
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

            if (number < 1 || number > configs.Count)
            {
                await ctx.RespondAsync(_embed.Message("Cannot start a build: unknown build configuration.",
                    DiscordColor.Red));
            }
            
            var config = configs[number - 1];
            var startStatus = await ctx.RespondAsync(_embed.Message("Starting build...", DiscordColor.Blurple));

            if (!_buildService.StartBuild(config, ctx.User, out string errorMessage))
            {
                await startStatus.ModifyAsync(
                    _embed.Message($"An error occured while starting the build: {errorMessage}", DiscordColor.Red));
                return;
            }

            await startStatus.ModifyAsync(_embed.Message("Successfully started build.", DiscordColor.Green));
        }

        [Command("cancelbuild")]
        [Aliases("cancel")]
        public async Task CancelBuild(CommandContext ctx)
        {
            if (!_buildService.IsBuilding())
            {
                await ctx.RespondAsync(_embed.Message("There are no builds to cancel.", DiscordColor.Red));
                return;
            }

            if (_buildService.IsCancellationRequested())
            {
                await ctx.RespondAsync(_embed.Message("Cancellation was already requested.", DiscordColor.Red));
                return;
            }

            await _buildService.CancelBuildAsync();
            await ctx.RespondAsync(_embed.Message("Cancellation request successful, please standby.",
                DiscordColor.Green));
        }
        
        [Command("downloadconfig")]
        [Aliases("dlconfig", "download")]
        public async Task DownloadConfig(CommandContext ctx)
        {
            var configs = _buildService.GetBuildConfigurations();
            if (configs.Count == 0)
            {
                await ctx.RespondAsync(_embed.Message("There are no configurations to download.", DiscordColor.Red));
                return;
            }
            
            var sb = new StringBuilder();
            for (int i = 0; i < configs.Count; i++)
            {
                sb.AppendLine($"{i + 1} - {configs[i].Name}");
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle("UnrealBuildTool - Download Build Configuration")
                .WithDescription("Please type the number of the build configuration to download.")
                .WithColor(DiscordColor.Blurple)
                .AddField("**Available Configurations**", sb.ToString())
                .Build();

            await ctx.RespondAsync(embed);

            var interactivity = ctx.Client.GetInteractivity();

            var result = await interactivity.WaitForMessageAsync(m => m.Author.Id == ctx.User.Id,
                TimeSpan.FromMinutes(1));

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
            
            using (var stream = File.OpenRead($"config/buildConfigurations/{config.SourceFile}"))
            {
                var builder = new DiscordMessageBuilder()
                    .WithEmbed(_embed.Message("Here you go.", DiscordColor.Green))
                    .WithFile(config.SourceFile, stream);

                await ctx.Channel.SendMessageAsync(builder);
            }
        }
        
        [Command("uploadconfig")]
        [Aliases("upload")]
        public async Task UploadConfig(CommandContext ctx)
        {
            if (!ctx.Message.Attachments.Any())
            {
                await ctx.RespondAsync(_embed.Message(
                    "Please send a valid configuration file as an attachment to your command.", DiscordColor.Red));
                return;
            }

            var file = ctx.Message.Attachments[0];
            if (!file.FileName.ToLower().EndsWith(".json"))
            {
                await ctx.RespondAsync(_embed.Message("File must be a valid .json file.", DiscordColor.Red));
                return;
            }
            
            var response = await _httpClient.GetAsync(file.Url);
            if (!response.IsSuccessStatusCode)
            {
                await ctx.RespondAsync(
                    _embed.Message($"Failed to download configuration. HTTP Error Code '{response.StatusCode}'", DiscordColor.Red));
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            
            try
            {
                var buildConfig = JsonConvert.DeserializeObject<BuildConfiguration>(json);
            }
            catch (Exception e)
            {
                await ctx.RespondAsync(_embed.Message($"Failed to parse json into BuildConfiguration: {e.Message}.",
                    DiscordColor.Red));
                return;
            }

            await File.WriteAllTextAsync($"config/buildConfigurations/{file.FileName}", json);
            await ctx.RespondAsync(_embed.Message($"Saved configuration to {file.FileName}. Don't forget to reload configs.", DiscordColor.Green)); 
        }

        [Command("configtemplate")]
        public async Task GetBuildConfigTemplate(CommandContext ctx)
        {
            using (var stream = File.OpenRead("config/buildTemplate.json"))
            {
                var builder = new DiscordMessageBuilder()
                    .WithEmbed(_embed.Message("Here you go.", DiscordColor.Green))
                    .WithFile("config/buildTemplate.json", stream);
                
                await ctx.RespondAsync(builder);
            }
        }

        [Command("stagetemplate")]
        public async Task GetStageTemplates(CommandContext ctx)
        {
            using var stream = File.OpenRead("config/stageTemplates.json");
            var builder = new DiscordMessageBuilder()
                .WithEmbed(_embed.Message("Here you go.", DiscordColor.Green))
                .WithFile("config/stageTemplates.json", stream);
                
            await ctx.RespondAsync(builder);
        }
    }
}