using System;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using UnrealBuildTool.Services;

namespace UnrealBuildTool
{
    public class UnrealBuildTool
    {
        public static readonly string LogCategory = "UnrealBuildTool: ";

        private CommandsNextExtension _commands;
        private ConfigurationService _config;
        private DiscordClient _client;
        private Logger _log;
        private IServiceProvider _services;
        
        public async Task StartAsync(string[] args)
        {
            // Generate or load the configuration file.
            if (!ConfigurationService.Exists())
            {
                Console.WriteLine("Could not locate config.json in root directory, generating a new one.");
                var config = new ConfigurationService();

                try
                {
                    await config.SaveConfigurationAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"An error has occured while generating a configuration file: {e.Message}");
                    Console.ReadLine();
                    Environment.Exit(1);
                }
                
                Console.WriteLine("Successfully generated configuration file at config.json, please modify it before restarting the bot.");
                Console.ReadLine();
                Environment.Exit(0);
            }

            _config = await ConfigurationService.LoadConfigurationAsync();
            if (!_config.IsConfigurationValid(out string errorMessage))
            {
                Console.WriteLine($"An error occured while loading the configuration file: \"{errorMessage}\"");
                Console.ReadLine();
                Environment.Exit(1);
            }
            
            // Create the logger.
            _log = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .CreateLogger();

            Log.Logger = _log;
            
            // Initialize the Discord client and commands service.
            _client = new DiscordClient(new DiscordConfiguration
            {
                Token = _config.Discord.Token,
                TokenType = TokenType.Bot,
                LoggerFactory = new LoggerFactory().AddSerilog(),
            });

            _services = GetServices();

            InitializeServices();
            
            _log.Information(LogCategory + "Booting up bot.");

            _commands = _client.UseCommandsNext(new CommandsNextConfiguration
            {
                Services = _services,
                StringPrefixes = new[] {_config.Discord.Prefix},
                EnableDms = true,
                EnableMentionPrefix = true,
                EnableDefaultHelp = false,
                CaseSensitive = false,
                IgnoreExtraArguments = true,
            });

            _client.UseInteractivity(new InteractivityConfiguration());
            _commands.RegisterCommands(Assembly.GetEntryAssembly());
            _services.GetRequiredService<DiscordEventHandler>().SetupCommandHandlers(_commands);

            await _client.ConnectAsync();

            // Delay this task forever, stopping the program from ending.
            await Task.Delay(-1);
        }

        public IServiceProvider GetServices()
        {
            var services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_config)
                .AddSingleton(_log)
                .AddSingleton<DiscordEventHandler>()
                .AddSingleton<EvaluationService>()
                .AddSingleton<EmbedService>()
                .AddSingleton<BuildService>();

            return services.BuildServiceProvider();
        }

        public void InitializeServices()
        {
            _services.GetRequiredService<DiscordEventHandler>();

            // This is done last, since the embed service is likely required by others first, and it may require others itself.
            _services.GetRequiredService<EmbedService>().InjectServices(_services);
        }
    }
}