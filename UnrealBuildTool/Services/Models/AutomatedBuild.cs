﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using UnrealBuildTool.Services;

namespace UnrealBuildTool.Build
{
    public class AutomatedBuild
    {
        private readonly BuildConfiguration _buildConfig;
        private readonly BuildService _buildService;
        private readonly DiscordUser _instigator;
        private readonly IServiceProvider _services;

        private List<BuildStage> _stages = new List<BuildStage>();

        private int _currentStage = 0;
        private DateTimeOffset _buildStartTime;
        private bool _isCompleted = false;
        private bool _isFailed = false;
        private bool _isCancelled = false;
        private bool _isCancellationRequested = false;
        private bool _backgroundStageFailed = false;

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
        /// Raised when the current build is cancelled.
        /// </summary>
        public Action OnCancelled { get; set; }

        /// <summary>
        /// Raised when the current build sends new console output.
        /// </summary>
        public Action<string> OnConsoleOutput { get; set; }

        /// <summary>
        /// Raised when the current build sends new console errors.
        /// </summary>
        public Action<string> OnConsoleError { get; set; }

        public AutomatedBuild(IServiceProvider services, BuildConfiguration configuration, DiscordUser instigator)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _buildService = services.GetRequiredService<BuildService>();
            _buildConfig = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _instigator = instigator;
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
            return _isFailed || _backgroundStageFailed;
        }

        public bool IsCancelled()
        {
            return _isCancelled && !_backgroundStageFailed;
        }

        public bool IsCancellationRequested()
        {
            return _isCancellationRequested;
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
            foreach (var stage in _buildConfig.Stages)
            {
                if (!_buildService.StageExists(stage.Name))
                {
                    ErrorMessage = $"Configuration contains unknown stage '{stage}'.";
                    return false;
                }

                var instancedStage = _buildService.InstantiateStage(stage.Name);
                if (instancedStage == null)
                {
                    ErrorMessage = $"Failed to instantiate stage '{stage}'.";
                    return false;
                }

                instancedStage.GenerateDefaultStageConfiguration();
                instancedStage.SetStageConfiguration(stage.Configuration);
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

        public async Task CancelBuildAsync()
        {
            if (_buildTask == null)
            {
                throw new InvalidOperationException("No build is running, cannot cancel build.");
            }

            _isCancellationRequested = true;
            _cancellationToken.Cancel();
            int currentStage = _currentStage;
            if (currentStage < _stages.Count)
            {
                var stage = _stages[currentStage];
                await stage.OnCancellationRequestedAsync();
            }
        }

        private async Task StartBuildAsync_Internal()
        {
            while (_currentStage < _stages.Count && !_cancellationToken.IsCancellationRequested)
            {
                OnStagedChanged();

                var stage = _stages[_currentStage];
                OnConsoleOutput($"UBT: Starting stage '{stage.GetName()}'");

                stage.LogBuilder = new StringBuilder();
                stage.OnConsoleOut += OnConsoleOutput;
                stage.OnConsoleError += OnConsoleError;

                // Check if the new stage can run with current background stages.
                var runningBackgroundStages = _stages
                    .Where(s => s.RunInBackground())
                    .Where(s => s.StageResult == StageResult.Running)
                    .ToList();

                var forbiddenStages = stage.GetIncompatibleBackgroundStages(runningBackgroundStages);

                if (forbiddenStages.Any())
                {
                    OnConsoleOutput(
                        $"UBT: Stage '{stage.GetName()}' cannot run with some background stages. Waiting for background stages to end.");
                    var tasks = forbiddenStages
                        .Where(b => b.BackgroundTask != null)
                        .Select(s => s.BackgroundTask)
                        .ToArray();

                    Task.WaitAll(tasks);
                }

                stage.StageResult = StageResult.Running;

                // Run it in the background if it wants to.
                if (stage.RunInBackground())
                {
                    stage.BackgroundTask = Task.Run(async () =>
                    {
                        try
                        {
                            stage.StageResult = await stage.DoTaskAsync(_services);
                        }
                        catch (Exception e)
                        {
                            stage.StageResult = StageResult.Failed;
                            stage.FailureReason = "An exception has occured during background execution: " + e.Message;
                        }

                        stage.OnConsoleOut -= OnConsoleOutput;
                        stage.OnConsoleError -= OnConsoleError;

                        // Check if it wrote anything to the log stream, send the result to Discord if it did.
                        if (stage.LogBuilder.Length != 0)
                        {
                            await _buildService.SendStageLogAsync(stage.GetName(), stage.LogBuilder);
                        }

                        // Oh no, it failed, so we'll kill everything.
                        if (stage.StageResult == StageResult.Failed)
                        {
                            _backgroundStageFailed = true;
                            await _buildService.CancelBuildAsync();
                        }
                    });

                    _currentStage++;
                    continue;
                }

                try
                {
                    stage.StageResult = await stage.DoTaskAsync(_services);
                }
                catch (Exception e)
                {
                    stage.StageResult = StageResult.Failed;
                    stage.FailureReason = "An exception has occured during execution: " + e.Message;
                }

                stage.OnConsoleOut -= OnConsoleOutput;
                stage.OnConsoleError -= OnConsoleError;

                // Check if it wrote anything to the log stream, send the result to Discord if it did.
                if (stage.LogBuilder.Length != 0)
                {
                    await _buildService.SendStageLogAsync(stage.GetName(), stage.LogBuilder);
                }

                if (stage.StageResult == StageResult.Running || stage.StageResult == StageResult.Scheduled)
                {
                    stage.FailureReason = "Stage returned 'Running' or 'Scheduled' stage result, and was failed for it.";
                    stage.StageResult = StageResult.Failed;
                }

                if (_cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (stage.StageResult == StageResult.Failed)
                {
                    _isFailed = true;
                    OnFailed(stage);

                    // Cancel any ongoing background stages
                    var ongoing = _stages
                        .Where(s => s.BackgroundTask != null)
                        .Where(s => s.StageResult == StageResult.Running)
                        .ToList();

                    ongoing.ForEach(async s => await s.OnCancellationRequestedAsync());
                    await Task.WhenAll(ongoing.Select(s => s.BackgroundTask).ToArray());
                    return;
                }

                _currentStage++;
            }

            if (_cancellationToken.IsCancellationRequested)
            {
                _isCancelled = true;
                OnCancelled();
            }

            // Wait for all background tasks to finish.
            var backgroundStages = _stages.Where(s => s.RunInBackground() && s.BackgroundTask != null).ToList();
            await Task.WhenAll(backgroundStages.Select(s => s.BackgroundTask).ToArray());

            if (_isCancelled)
            {
                return;
            }

            if (backgroundStages.Any(s => s.StageResult == StageResult.Failed))
            {
                _isFailed = true;
                OnFailed(null);
                return;
            }

            if (_currentStage == _stages.Count)
            {
                _isCompleted = true;
                OnCompleted();
                return;
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
    }
}