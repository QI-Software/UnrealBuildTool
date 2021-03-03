using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace UnrealBuildTool.Build
{
    /// <summary>
    /// Denotes a single stage of a build.
    /// </summary>
    public abstract class BuildStage
    {
        private List<StageConfigurationKey> _defaultStageConfig;
        
        /// <summary>
        /// The name of the stage as it will be referred to in build configurations.
        /// </summary>
        public abstract string GetName();

        /// <summary>
        /// The description of the stage to output to the user. 
        /// </summary>
        public abstract string GetDescription();

        /// <summary>
        /// The required configuration arguments for this stage. The default values will be displayed on templates.
        /// </summary>
        [JsonProperty]
        protected internal Dictionary<string, object> StageConfiguration { get; internal set; } = new Dictionary<string, object>();

        /// <summary>
        /// Is the set stage configuration valid? 
        /// </summary>
        /// <param name="ErrorMessage"> If non valid, the message to display to the user before failing the build. </param>
        public virtual bool IsStageConfigurationValid(out string ErrorMessage)
        {
            foreach (var defaultKey in _defaultStageConfig)
            {
                if (!StageConfiguration.ContainsKey(defaultKey.Key))
                {
                    ErrorMessage = $"Did not find expected configuration key '{defaultKey.Key}'.";
                    return false;
                }

                if (!defaultKey.Type.IsInstanceOfType(StageConfiguration[defaultKey.Key]))
                {
                    ErrorMessage = $"Expected key '{defaultKey.Key}' to be of type '{defaultKey.Type}'.";
                    return false;
                }
            }

            ErrorMessage = null;
            return true;
        }

        /// <summary>
        /// Override to add default stage configurations, using AddDefaultConfigurationKey()
        /// </summary>
        public virtual void GenerateDefaultStageConfiguration()
        {
            _defaultStageConfig = new List<StageConfigurationKey>();
        }

        public virtual void AddDefaultConfigurationKey(string keyName, Type type, object defaultValue)
        {
            if (string.IsNullOrWhiteSpace(keyName))
            {
                throw new ArgumentException($"Argument '{nameof(keyName)}' cannot be null or whitespace.");
            }

            if (type == null)
            {
                throw new ArgumentNullException($"Argument '{nameof(type)}' cannot be null.");
            }
            
            var defaultKey = new StageConfigurationKey(keyName, type, defaultValue);
            _defaultStageConfig.Add(defaultKey);
            StageConfiguration.Add(keyName, defaultValue);
        }
        
        public virtual Dictionary<string, object> GetStageConfiguration()
        {
            return StageConfiguration;
        }
    }
}