using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using UnrealBuildTool.Preconditions;
using UnrealBuildTool.Services;

namespace UnrealBuildTool.Commands
{
    [RequireMaintainer]
    public class DebugCommands : BaseCommandModule
    {
        private readonly EmbedService _embedService;
        private readonly EvaluationService _evalService;
        private Process _lastCmdProcess = null; 
        
        public DebugCommands(EmbedService embed, EvaluationService evalService)
        {
            _embedService = embed;
            _evalService = evalService;
        }
        
        [Command("eval")]
        public async Task EvaluateCode(CommandContext ctx, [RemainingText] string code)
        {
            var watch = new Stopwatch();
            code = code.Replace("```cs", "").Replace("```", "");
            watch.Start();

            var formattedMessage = Formatter.BlockCode("Evaluating...", "diff");

            var msg = await ctx.RespondAsync(formattedMessage);
            
            var result = await _evalService.EvaluateAsync(ctx, code);

            watch.Stop();

            formattedMessage = Formatter.BlockCode(
                $"+ Evaluated in {watch.ElapsedMilliseconds} ms\n"
                     + $"+ Result: {(result.Result ?? result.Exception)?.GetType()?.Name ?? "void"}\n\n"
                     + $"{result.Result ?? result.Exception.Message}\n", "diff");

            await msg.ModifyAsync(formattedMessage);
        }

        [Command("exec")]
        [Aliases("cmd")]
        public async Task RunShellCommand(CommandContext ctx, [RemainingText] string command)
        {
            if (_lastCmdProcess != null)
            {
                await ctx.RespondAsync(_embedService.Message(
                    "Cannot run shell command: a shell process is already running, please use the kill command to stop it.",
                    DiscordColor.Red));
                return;
            }
            
            _lastCmdProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/C " + command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            var output = new List<string>();
            _lastCmdProcess.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    output.Add(args.Data);
                }
            };
            
            _lastCmdProcess.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    output.Add($"STDERR> {args.Data}");
                }
            };

            _lastCmdProcess.Start();
            _lastCmdProcess.BeginOutputReadLine();
            _lastCmdProcess.BeginErrorReadLine();
            _lastCmdProcess.WaitForExit();

            var grabbedInput = new List<string>();
            var len = 0;
            for (int i = output.Count - 1; i >= 0; i--)
            {
                var str = output[i];
                if (len + str.Length < 1990)
                {
                    len += str.Length;
                    grabbedInput.Add(str);
                }
                else
                {
                    break;
                }
            }

            var sb = new StringBuilder();
            for (int i = grabbedInput.Count - 1; i >= 0; i--)
            {
                sb.Append(grabbedInput[i]);
            }
            
            await ctx.RespondAsync(_embedService.Message($"Process exited with code '{_lastCmdProcess.ExitCode}'.",
                _lastCmdProcess.ExitCode == 0 ? DiscordColor.Green : DiscordColor.Red));
            _lastCmdProcess = null;

            if (sb.Length == 0)
            {
                return;
            }
            
            var formattedOut = Formatter.BlockCode(sb.ToString());
            await ctx.RespondAsync(formattedOut);
        }

        [Command("kill")]
        public async Task KillShell(CommandContext ctx)
        {
            if (_lastCmdProcess != null && !_lastCmdProcess.HasExited)
            {
                _lastCmdProcess.Kill(true);
                await ctx.RespondAsync(_embedService.Message("Killed shell instance.", DiscordColor.Green));
            }
            else
            {
                await ctx.RespondAsync(_embedService.Message("There is no shell running at the moment.",
                    DiscordColor.Red));
            }
        }

        [Command( "interactive" )]
        public async Task InteractiveDebug( CommandContext ctx )
        {
            await ctx.RespondAsync( _embedService.Message( "Interactive C# Mode, 'stop' to end session.", DiscordColor.Green ) );

            var interactivity = ctx.Client.GetInteractivity();
            var result = await interactivity.WaitForMessageAsync( msg => msg.Author.Id == ctx.User.Id && msg.Channel.Id == ctx.Channel.Id, TimeSpan.FromMinutes( 10 ) );

            while (!result.TimedOut && result.Result.Content.ToLower() != "stop")
            {
                await EvaluateCode( ctx, result.Result.Content );
            }

            await ctx.RespondAsync( _embedService.Message( "Interative C# session finished.", DiscordColor.Green ) );
        }
    }
}