﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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
        /// Thw build config this build stage belongs to, set before executing the task.
        /// </summary>
        public BuildConfiguration BuildConfig { get; set; }
        
        /// <summary>
        /// The reason the stage failed if it did.
        /// </summary>
        public string FailureReason { get; set; }
        
        /// <summary>
        /// Raised when this build stage receives new console output.
        /// </summary>
        public Action<string> OnConsoleOut;
        
        /// <summary>
        /// Raised when this build stage receives a new console error.
        /// </summary>
        public Action<string> OnConsoleError;

        /// <summary>
        /// Anything added to this string builder will be sent as a file at the end of the build stage.
        /// </summary>
        public StringBuilder LogBuilder;

        /// <summary>
        /// The required configuration arguments for this stage. The default values will be displayed on templates.
        /// </summary>
        [JsonProperty]
        protected internal Dictionary<string, object> StageConfiguration { get; internal set; } = new Dictionary<string, object>();

        /// <summary>
        /// The result of this stage.
        /// </summary>
        public StageResult StageResult { get; set; } = StageResult.Scheduled;
        
        /// <summary>
        /// Whether or now cancellation was called while this task was running.
        /// </summary>
        public bool IsCancelled { get; private set; }
        
        /// <summary>
        /// If this task is currently running in the background, the task it runs in.
        /// </summary>
        public Task BackgroundTask { get; set; }

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
                    ErrorMessage = $"Stage '{GetName()}': did not find expected configuration key '{defaultKey.Key}'.";
                    return false;
                }

                if (!defaultKey.Type.IsInstanceOfType(StageConfiguration[defaultKey.Key]))
                {
                    if (!(IsNumericType(defaultKey.Type) &&
                          IsNumericType(StageConfiguration[defaultKey.Key].GetType())))
                    {
                        ErrorMessage = $"Stage '{GetName()}': expected key '{defaultKey.Key}' to be of type '{defaultKey.Type}'.";
                        return false;
                    }
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

        public void AddDefaultConfigurationKey<T>(string keyName, T defaultValue)
        {
            if (string.IsNullOrWhiteSpace(keyName))
            {
                throw new ArgumentException($"Argument '{nameof(keyName)}' cannot be null or whitespace.");
            }

            var defaultKey = new StageConfigurationKey(keyName, typeof(T), defaultValue);
            _defaultStageConfig.Add(defaultKey);
            StageConfiguration.Add(keyName, defaultValue);
        }
        
        public Dictionary<string, object> GetStageConfiguration()
        {
            return StageConfiguration;
        }

        public void SetStageConfiguration(Dictionary<string, object> newConfig)
        {
            StageConfiguration = newConfig ?? throw new ArgumentNullException(nameof(newConfig));
        }

        public bool TryGetConfigValue(string key, out bool value)
        {
            if (StageConfiguration.ContainsKey(key))
            {
                if (StageConfiguration[key] is bool b)
                {
                    value = b;
                    return true;
                }
            }

            value = default;
            return false;
        }
        
        public bool TryGetConfigValue(string key, out string value)
        {
            if (StageConfiguration.ContainsKey(key))
            {
                if (StageConfiguration[key] is string str)
                {
                    value = str;
                    return true;
                }
            }

            value = default;
            return false;
        }
        
        public bool TryGetConfigValue(string key, out int value)
        {
            if (StageConfiguration.ContainsKey(key))
            {
                if (int.TryParse(StageConfiguration[key].ToString(), out value))
                {
                    return true;
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Called to execute this build stage's task. Return whether or not the task succeeded.
        /// </summary>
        /// <param name="services"></param>
        public abstract Task<StageResult> DoTaskAsync(IServiceProvider services);

        /// <summary>
        /// Called when the build needs to be cancelled. Release anything ongoing and halt as soon as possible.
        /// </summary>
        public virtual Task OnCancellationRequestedAsync()
        {
            IsCancelled = true;
            OnConsoleOut("UBT: Received cancellation request.");
            return Task.CompletedTask;
        }

        /// <summary>
        /// If true, this build stage will run on a background task, allowing other tasks to start at the same time.
        /// </summary>
        public virtual bool RunInBackground()
        {
            return false;
        }

        /// <summary>
        /// Returns the background stages this stage cannot run with.
        /// </summary>
        public virtual List<BuildStage> GetIncompatibleBackgroundStages(List<BuildStage> stages)
        {
            return new List<BuildStage>();
        }
        
        private bool IsNumericType(Type type)
        {   
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }
    }
}