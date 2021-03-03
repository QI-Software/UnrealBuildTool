using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Serilog.Core;

namespace UnrealBuildTool.Services
{
    public class DiscordEventHandler
    {
        private readonly DiscordClient _client;
        private readonly Logger _log;

        public DiscordEventHandler(DiscordClient client, Logger log)
        {
            _client = client;
            _log = log;
            
            _client.Ready += OnReady;
        }
        
        public void SetupCommandHandlers(CommandsNextExtension commands)
        {
            commands.CommandExecuted += CommandExecuted;
            commands.CommandErrored += CommandErrored;
        }

        private async Task OnReady(DiscordClient sender, ReadyEventArgs e)
        {
            await _client.UpdateStatusAsync(new DiscordActivity("Ready to build!", ActivityType.Playing),
                UserStatus.Online);
        }
        
        private Task CommandErrored(CommandsNextExtension commands, CommandErrorEventArgs e)
        {
            DiscordUser user = e.Context.User;

            if (e.Command == null)
            {
                return Task.CompletedTask;
            }
            
            _log.Warning(e.Exception, $"User {user.Username}#{user.Discriminator} failed to execute command: {(e.Command?.Parent?.Name ?? string.Empty) + " "}{e.Command?.Name ?? "UNKNOWN"}.");
            return Task.CompletedTask;
        }

        private Task CommandExecuted(CommandsNextExtension commands, CommandExecutionEventArgs e)
        {
            DiscordUser user = e.Context.User;
            _log.Information($"User {user.Username}#{user.Discriminator} successfully executed command: {(e.Command?.Parent?.Name ?? string.Empty) + " "}{e.Command?.Name ?? string.Empty}.");
            return Task.CompletedTask;
        }
    }
}