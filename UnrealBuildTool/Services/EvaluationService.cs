using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;
using UnrealBuildTool.Models;

namespace UnrealBuildTool.Services
{
    public class EvaluationService
    {
        private IEnumerable<Assembly> _assemblies => GetAssemblies();
        private readonly IEnumerable<string> _imports = new List<string> 
        {
            "DSharpPlus",
            "DSharpPlus.Entities",
            "DSharpPlus.CommandsNext",
            "Microsoft.Extensions.DependencyInjection",
            "System",
            "System.Collections",
            "System.Collections.Generic",
            "System.Diagnostics",
            "System.IO",
            "System.Linq",
            "System.Math",
            "System.Reflection",
            "System.Runtime",
            "System.Threading.Tasks",
            "UnrealBuildTool",
            "UnrealBuildTool.Services",
            "Newtonsoft.Json"
        };

        public async Task<EvaluationResult> EvaluateAsync(CommandContext context, string script)
        {
            var options = ScriptOptions.Default
                .AddReferences(_assemblies)
                .AddImports(_imports);
            
            var globals = new ScriptGlobals { Context = context, Services = context.Services };

            try
            {
                var eval = await CSharpScript.EvaluateAsync(script, options, globals, typeof(ScriptGlobals));
                return new EvaluationResult(eval, null);
            }
            catch (Exception e)
            {
                return new EvaluationResult(null, e);
            }
        }
        private static IEnumerable<Assembly> GetAssemblies()
        {
            var referencedAssemblies = Assembly.GetEntryAssembly()?.GetReferencedAssemblies() ?? new AssemblyName[]{};
            foreach (var a in referencedAssemblies)
            {
                var asm = Assembly.Load(a);
                yield return asm;
            }
            
            yield return Assembly.GetEntryAssembly();
            yield return typeof(ILookup<string, string>).GetTypeInfo().Assembly;
        }
    }

    public class ScriptGlobals
    {
        public IServiceProvider Services { get; internal set; }
        public DiscordChannel Channel => Context.Channel;
        public DiscordClient Client => Context.Client;
        public CommandContext Context { get; internal set; }
        public DiscordGuild Guild => Context.Guild;
        public DiscordMessage Msg => Context.Message;

        public string Serialize(object input) => JsonConvert.SerializeObject(input, Formatting.Indented);
    }
}