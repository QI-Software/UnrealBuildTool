using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using UnrealBuildTool.Services;

namespace UnrealBuildTool.Build
{
    public class AutomatedBuild
    {
        private readonly BuildConfiguration _buildConfig;
        private readonly BuildService _buildService;
        private readonly DiscordUser _instigator;

        private List<BuildStage> _stages = new List<BuildStage>();
        private int _currentStage = 0;
        private DateTimeOffset _buildStartTime;
        private bool _isCompleted = false;
        private bool _isFailed = false;

        private Task _buildTask;
        private CancellationTokenSource _cancellationToken;

        /// <summary>
        /// Raised when the current build stage changes.
        /// </summary>
        public Action OnStagedChanged { get; set; }
        
        /// <summary>
        /// Raised when the current build completed successfully.
        /// </summary>
        public Action OnCompleted { get; set; }
        
        /// <summary>
        /// Raised when the current build failed.
        /// </summary>
        public Action<BuildStage> OnFailed { get; set; }
        
        /// <summary>
        /// Raised when the current build sends new console output.
        /// </summary>
        public Action<string> OnConsoleOutput { get; set; }
        
        /// <summary>
        /// Raised when the current build sends new console errors.
        /// </summary>
        public Action<string> OnConsoleError { get; set;  }

        public AutomatedBuild(BuildService svc, BuildConfiguration configuration, DiscordUser instigator)
        {
            if (configuration == null)
            {
                throw new NullReferenceException("Cannot instantiate a Build with a null BuildConfiguration.");
            }

            _buildService = svc;
            _buildConfig = configuration;
            _instigator = instigator;
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

                instancedStage.BuildConfig = _buildConfig;

                _stages.Add(instancedStage);
            }

            ErrorMessage = null;
            return true;
        }

        public void StartBuild()
        {
            if (_buildTask != null)
            {
                throw new InvalidOperationException(
                    "Attempted to re-use AutomatedBuild object, these are not meant to be re-used. Please make a new one.");
            }

            _cancellationToken = new CancellationTokenSource();
            _buildTask = Task.Run(async () => await StartBuildAsync_Internal(), _cancellationToken.Token);
            _buildStartTime = DateTimeOffset.Now;
        }

        public void CancelBuild()
        {
            if (_buildTask == null)
            {
                throw new InvalidOperationException("No build is running, cannot cancel build.");
            }

            _cancellationToken.Cancel();
        }

        private async Task StartBuildAsync_Internal()
        {
            while (_currentStage < _stages.Count && !_cancellationToken.IsCancellationRequested)
            {
                OnStagedChanged();

                var stage = _stages[_currentStage];
                OnConsoleOutput($"UBT: Starting stage '{stage.GetName()}'");

                stage.OnConsoleOut += OnConsoleOutput;
                stage.OnConsoleError += OnConsoleError;

                try
                {
                    stage.StageResult = await stage.DoTaskAsync();
                }
                catch (Exception e)
                {
                    stage.StageResult = StageResult.Failed;
                    stage.FailureReason = "An exception has occured during execution: " + e.Message;
                }

                stage.OnConsoleOut -= OnConsoleOutput;
                stage.OnConsoleError -= OnConsoleError;

                if (stage.StageResult == StageResult.Running)
                {
                    stage.StageResult = StageResult.Failed;
                }
                
                if (stage.StageResult == StageResult.Failed)
                {
                    _isFailed = true;
                    OnFailed(stage);
                    return;
                }

                _currentStage++;
            }

            if (_currentStage == _stages.Count)
            {
                _isCompleted = true;
                OnCompleted();
            }

            _isFailed = true;
            OnFailed(null);
        }

        public DiscordUser GetInstigator()
        {
            return _instigator;
        }

        public int GetCurrentStageIndex()
        {
            return _currentStage;
        }

        public List<BuildStage> GetStages()
        {
            return _stages;
        }

        public BuildStage GetCurrentStage()
        {
            if (_stages.Count > _currentStage)
            {
                return _stages[_currentStage];
            }

            return null;
        }

        public bool IsStarted()
        {
            return _buildTask != null;
        }

        public BuildConfiguration GetConfiguration()
        {
            return _buildConfig;
        }

        public DateTimeOffset GetStartTime()
        {
            return _buildStartTime;
        }

        public bool IsCompleted()
        {
            return _isCompleted;
        }

        public bool IsFailed()
        {
            return _isFailed;
        }
    }
}