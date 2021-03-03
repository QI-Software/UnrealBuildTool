using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnrealBuildTool.Services;

namespace UnrealBuildTool.Build
{
    public class AutomatedBuild
    {
        private readonly BuildConfiguration _buildConfig;
        private readonly BuildService _buildService;

        private List<BuildStage> _stages = new List<BuildStage>();
        private int _currentStage = 0;
        
        public AutomatedBuild(BuildService svc, BuildConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new NullReferenceException("Cannot instantiate a Build with a null BuildConfiguration.");
            }

            _buildService = svc;
            _buildConfig = configuration;
        }

        public bool InitializeConfiguration(out string ErrorMessage)
        {
            if (_buildConfig == null)
            {
                ErrorMessage = "Cannot start build with a null configuration.";
                return false;
            }

            if (_buildConfig.Stages.Count == 0)
            {
                ErrorMessage = "Cannot start a build without any build stages.";
                return false;
            }
            
            // Initialize the required build stages.
            foreach (var stage in _buildConfig.Stages.Keys)
            {
                if (!_buildService.StageExists(stage))
                {
                    ErrorMessage = $"Configuration contains unknown stage '{stage}'.";
                    return false;
                }

                var instancedStage = _buildService.InstantiateStage(stage);
                if (instancedStage == null)
                {
                    ErrorMessage = $"Failed to instantiate stage '{stage}'.";
                    return false;
                }
                
                instancedStage.GenerateDefaultStageConfiguration();
                if (!instancedStage.IsStageConfigurationValid(out ErrorMessage))
                {
                    return false;
                }

                _stages.Add(instancedStage);
            }

            ErrorMessage = null;
            return true;
        }
    }
}