using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using Microsoft.CodeAnalysis.Diagnostics;
using Newtonsoft.Json;
using Serilog;
using Serilog.Core;
using UnrealBuildTool.Build;

namespace UnrealBuildTool.Services
{
    public class BuildService
    {
        public static readonly string LogCategory = "BuildService: ";

        private readonly BuildNotificationService _buildNotifier;
        private readonly Logger _log;

        private AutomatedBuild _currentBuild = null;
        private Dictionary<string, Type> _buildStages = new Dictionary<string, Type>();
        private List<BuildConfiguration> _buildConfigurations = new List<BuildConfiguration>();

        public BuildService(BuildNotificationService notifier, Logger log)
        {
            _buildNotifier = notifier;
            _log = log;
        }

        public async Task InitializeAsync()
        {
            LoadBuildStages();
            await GenerateBuildStageTemplatesAsync();

            await LoadBuildConfigurationsAsync();
        }

        public bool IsBuilding()
        {
            return _currentBuild != null;
        }

        public List<BuildConfiguration> GetBuildConfigurations()
        {
            return _buildConfigurations;
        }

        /// <summary>
        /// Loads all of the build stages using reflection.
        /// </summary>
        public void LoadBuildStages()
        {
            _buildStages = new Dictionary<string, Type>();

            var reflectedStages = typeof(BuildStage)
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(BuildStage)));

            foreach (var stageType in reflectedStages)
            {
                var stage = (BuildStage) Activator.CreateInstance(stageType);
                
                if (stage != null)
                {
                    var name = stage.GetName();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        if (!_buildStages.ContainsKey(name))
                        {
                            _buildStages.Add(name, stageType);
                            _log.Information(LogCategory + $"Found build stage {name}.");
                        }
                        else
                        {
                            _log.Warning(LogCategory + $"Found duplicate stage with name {name}, ignoring.");
                        }
                    }
                    else
                    {
                        _log.Error(LogCategory + $"Stage type {stageType.Name} returns a null or whitespace string.");
                    }
                }
            }
        }

        public async Task GenerateBuildStageTemplatesAsync()
        {
            if (!Directory.Exists("config/"))
            {
                Directory.CreateDirectory("config/");
            }
            
            var templates = new List<BuildStageTemplate>();
            
            foreach (var stageName in _buildStages.Keys)
            {
                var stage = InstantiateStage(stageName);

                if (stage != null)
                {
                    stage.GenerateDefaultStageConfiguration();
                    var template = new BuildStageTemplate(stage);
                    templates.Add(template);
                }
            }

            var json = JsonConvert.SerializeObject(templates, Formatting.Indented);
            await File.WriteAllTextAsync("config/stageTemplates.json", json);
        }

        public bool StageExists(string stageName)
        {
            return _buildStages.ContainsKey(stageName) && _buildStages[stageName] != null;
        }

        public BuildStage InstantiateStage(string stageName)
        {
            if (string.IsNullOrWhiteSpace(stageName))
            {
                throw new InvalidOperationException($"Cannot instantiate invalid stage name '{stageName ?? "null"}'");
            }
            
            if (!StageExists(stageName))
            {
                throw new InvalidOperationException(
                    $"Could not find any stage with name '{stageName}' to instantiate.");
            }

            return (BuildStage) Activator.CreateInstance(_buildStages[stageName]);
        }

        public async Task LoadBuildConfigurationsAsync()
        {
            if (IsBuilding())
            {
                return;
            }

            _buildConfigurations = new List<BuildConfiguration>();

            if (!Directory.Exists("config/buildConfigurations"))
            {
                Directory.CreateDirectory("config/buildConfigurations");
            }

            var template = new BuildConfigurationTemplate();
            var templateJson = JsonConvert.SerializeObject(template, Formatting.Indented);
            await File.WriteAllTextAsync("config/buildTemplate.json", templateJson);

            foreach (var file in Directory.GetFiles("config/buildConfigurations", "*.json"))
            {
                var filename = Path.GetFileName(file);
                var json = await File.ReadAllTextAsync(file);
                BuildConfiguration config = null;
                
                try
                {
                    config = JsonConvert.DeserializeObject<BuildConfiguration>(json);
                }
                catch (Exception e)
                {
                    _log.Error(LogCategory + $"Failed to load build configuration from JSON file '{Path.GetFileName(file)}': " + e.Message);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(config.Name))
                {
                    _log.Warning(LogCategory + $"Build configuration '{filename}' has a null or empty name, ignoring.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(config.Description))
                {
                    _log.Warning(LogCategory + $"Build configuration '{config.Name}' has a null or empty description, ignoring.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(config.ProjectDirectory))
                {
                    _log.Warning(LogCategory + $"Build configuration '{config.Name}' has a null or empty project directory, ignoring.");
                    continue;
                }

                if (!Directory.Exists(config.ProjectDirectory))
                {
                    _log.Warning(LogCategory + $"Build configuration '{config.Name}' points to a non-existing directory. It must be the root directory of the project.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(config.ProjectFile))
                {
                    _log.Warning(LogCategory + $"Build configuration '{config.Name}' has null or empty project file, ignoring.");
                    continue;
                }

                var projectFilePath = $"{config.ProjectDirectory}/{config.ProjectFile}";
                projectFilePath = projectFilePath.Replace("//", "/");
                
                if (!File.Exists(projectFilePath))
                {
                    _log.Warning(LogCategory + $"Build configuration '{config.Name}' has no .uproject file at set location, ignoring.");
                    continue;
                }

                if (!Directory.Exists(config.EngineDirectory))
                {
                    _log.Warning(LogCategory + $"Build configuration '{config.Name}' points to a non-existing engine directory, ignoring.");
                    continue;
                }

                if (config.Stages.Count == 0)
                {
                    _log.Warning(LogCategory + $"Build configuration '{config.Name}' contains no build stages, ignoring.");
                    continue;
                }

                var unknownStage = config.Stages.FirstOrDefault(s => !StageExists(s.Name));
                if (unknownStage != default)
                {
                    _log.Warning(LogCategory + $"Build configuration '{config.Name}' contains an unknown build stage '{unknownStage}', ignoring.");
                    continue;
                }
                
                _log.Information(LogCategory + $"Loaded build configuration '{config.Name}'.");
                _buildConfigurations.Add(config);
            }
        }

        public bool StartBuild(BuildConfiguration configuration, DiscordUser user, out string ErrorMessage)
        {
            if (configuration == null)
            {
                ErrorMessage = "Cannot start a build with a null configuration";
                return false;
            }

            if (IsBuilding())
            {
                ErrorMessage = "Cannot start a build: already building.";
                return false;
            }

            if (!_buildNotifier.IsLinkedToChannel())
            {
                ErrorMessage = "Misconfigured build channel, the build notifier isn't able to output to the console.";
                return false;
            }

            var newBuild = new AutomatedBuild(this, configuration, user);
            if (!newBuild.InitializeConfiguration(out ErrorMessage))
            {
                return false;
            }

            _currentBuild = newBuild;
            _currentBuild.OnStagedChanged += OnStagedChanged;
            _currentBuild.OnConsoleOutput += OnConsoleOutput;
            _currentBuild.OnConsoleError += OnConsoleError;
            _currentBuild.OnCompleted += OnBuildCompleted;
            _currentBuild.OnFailed += OnBuildFailed;
            
            // Create the build status and output log messages on Discord.
            var task = _buildNotifier.InitializeBuildNotifications(_currentBuild);
            task.Wait();
            
            _currentBuild.StartBuild();
            return true;
        }

        private void OnBuildFailed(BuildStage failedStage)
        {
            _buildNotifier.OnBuildFailed();
            _currentBuild = null;
        }

        private void OnBuildCompleted()
        {
            _buildNotifier.OnBuildCompleted();
            _currentBuild = null;
        }

        private void OnConsoleError(string output)
        {
            _buildNotifier.AddOutputData(string.IsNullOrWhiteSpace(output) ? output : $"STDERR> {output}");
        }

        private void OnConsoleOutput(string output)
        {
            _buildNotifier.AddOutputData(output);
        }

        private async void OnStagedChanged()
        {
        }
    }
}