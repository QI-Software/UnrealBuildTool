using System.Diagnostics;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using UnrealBuildTool.Preconditions;
using UnrealBuildTool.Services;

namespace UnrealBuildTool.Commands
{
    [RequireMaintainer]
    public class DebugCommands : BaseCommandModule
    {
        private readonly EvaluationService _evalService;

        public DebugCommands(EvaluationService evalService)
        {
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
    }
}