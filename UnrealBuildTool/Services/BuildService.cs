using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using Serilog.Core;
using UnrealBuildTool.Build;

namespace UnrealBuildTool.Services
{
    public class BuildService
    {
        public static readonly string LogCategory = "BuildService: ";
        
        private readonly Logger _log;

        private AutomatedBuild _currentBuild;
        private Dictionary<string, Type> _buildStages = new Dictionary<string, Type>();

        public BuildService(Logger log)
        {
            _log = log;
        }

        public async Task InitializeAsync()
        {
            LoadBuildStages();
            await GenerateBuildStageTemplatesAsync();
        }

        public bool IsBuilding()
        {
            return _currentBuild != null;
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

        private bool StageExists(string stageName)
        {
            return _buildStages.ContainsKey(stageName) && _buildStages[stageName] != null;
        }

        private BuildStage InstantiateStage(string stageName)
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
    }
}