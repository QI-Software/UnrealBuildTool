using System;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using UnrealBuildTool.Preconditions;
using UnrealBuildTool.Services;
using UnrealBuildTool.Services.Models;

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

            await ctx.Member.SendMessageAsync(_embedService.Message("Please enter the name of the Steam account to use.", DiscordColor.Green));
            var result = await WaitForDMAsync(interactivity, ctx.Member, TimeSpan.FromMinutes(1));

            if (result.TimedOut)
            {
                await ctx.Member.SendMessageAsync(_embedService.Message("Timed out.", DiscordColor.Red));
                return;
            }
            
            var username = result.Result.Content.Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                await ctx.Member.SendMessageAsync(_embedService.Message("Invalid username specified, please restart this interactive command.", DiscordColor.Red));
                return;
            }


            if (_steamAuth.HasAccount(username))
            {
                await ctx.Member.SendMessageAsync(_embedService.Message("An account with this username already exists, please try again.", DiscordColor.Red));
                return;
            }
            
            await ctx.Member.SendMessageAsync(_embedService.Message("Please enter the password of the Steam account to use.", DiscordColor.Green));
            result = await WaitForDMAsync(interactivity, ctx.Member, TimeSpan.FromMinutes(1));

            if (result.TimedOut)
            {
                await ctx.Member.SendMessageAsync(_embedService.Message("Timed out.", DiscordColor.Red));
                return;
            }

            var password = result.Result.Content.Trim();
            if (string.IsNullOrWhiteSpace(password))
            {
                await ctx.Member.SendMessageAsync(_embedService.Message("Invalid password specified, please restart this interactive command.", DiscordColor.Red));
                return;
            }

            Func<Task<string>> TwoFactorHandler = async () =>
            {
                await ctx.Member.SendMessageAsync(_embedService.Message("Please enter the 2FA code you received by e-mail or SMS.", DiscordColor.Orange));
                var newResult = await WaitForDMAsync(interactivity, ctx.Member, TimeSpan.FromMinutes(5));
                if (newResult.TimedOut)
                {
                    return string.Empty;
                }

                return newResult.Result.Content?.Trim() ?? "";
            };

            var addResult = await _steamAuth.AddSteamworksUserAsync(username, password, TwoFactorHandler);
            if (addResult.Success)
            {
                await ctx.Member.SendMessageAsync(_embedService.Message($"Succesfully added new account '{username}'", DiscordColor.Green));
                await ctx.RespondAsync(_embedService.Message($"Succesfully added new account '{username}'", DiscordColor.Green));
            }
            else
            {
                await ctx.Member.SendMessageAsync(_embedService.Message($"Failed to add new account '{username}': {addResult.ErrorMessage}.", DiscordColor.Red));
                await ctx.RespondAsync(_embedService.Message($"Failed to add new account '{username}': {addResult.ErrorMessage}.", DiscordColor.Red));
            }
        }

        private async Task<InteractivityResult<DiscordMessage>> WaitForDMAsync(InteractivityExtension interactivity, DiscordMember member, TimeSpan timeout)
        {
            return await interactivity.WaitForMessageAsync(m => m.Author.Id == member.Id, timeout);
        }
    }
}