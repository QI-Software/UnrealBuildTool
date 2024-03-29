﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using Newtonsoft.Json;
using Serilog.Core;
using UnrealBuildTool.Services.Models;

namespace UnrealBuildTool.Services
{
    public class BuildScheduleService
    {
        public static readonly string LogCategory = "BuildSchedule: ";
        
        private List<BuildSchedule> _schedules;
        private Queue<BuildSchedule> _queuedBuilds;
        private CancellationTokenSource _backgroundScheduleSource;
        private Task _backgroundScheduleTask;

        private readonly BuildService _buildService;
        private readonly ConfigurationService _config;
        private readonly DiscordClient _client;
        private readonly Logger _log;

        public BuildScheduleService(BuildService buildService, ConfigurationService config, DiscordClient client, Logger log)
        {
            _buildService = buildService;
            _config = config;
            _client = client;
            _log = log;
        }

        public async Task InitializeAsync()
        {
            if (_backgroundScheduleTask != null && !_backgroundScheduleTask.IsCompleted)
            {
                throw new InvalidOperationException("Cannot initialize build scheduler: already initialized.");
            }
            
            _schedules = new List<BuildSchedule>();
            var buildConfigs = _buildService.GetBuildConfigurations();

            if (!Directory.Exists("config/schedules"))
            {
                Directory.CreateDirectory("config/schedules");
            }
            
            // Save schedule template.
            var template = new BuildSchedule
            {
                Name = "Template Schedule",
                ConfigurationName = "Build Configuration Name",
                StartDate = DateTimeOffset.UtcNow,
                RepeatInterval = TimeSpan.FromHours(1),
                NextRunDate = null,
            };

            try
            {
                var templateJson = JsonConvert.SerializeObject(template, Formatting.Indented);
                await File.WriteAllTextAsync("config/scheduleTemplate.json", templateJson);
            }
            catch (Exception e)
            {
                _log.Warning(LogCategory + $"Failed to save build schedule template: {e.Message}");
            }

            var files = Directory.GetFiles("config/schedules", "*.json", 
                SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                var json = await File.ReadAllTextAsync(file);
                BuildSchedule schedule;

                try
                {
                    schedule = JsonConvert.DeserializeObject<BuildSchedule>(json);
                }
                catch (Exception e)
                {
                    _log.Error(LogCategory + $"Failed to load build schedule from file '{file}': {e.Message}");
                    continue;
                }

                if (buildConfigs.All(b => b.Name.ToLower() != schedule.ConfigurationName.ToLower()))
                {
                    _log.Warning(LogCategory + $"Cannot load schedule '{file}': no build configuration with name '{schedule.ConfigurationName}'");
                    continue;
                }

                if (schedule.RepeatInterval.TotalMilliseconds <= 0)
                {
                    _log.Warning(LogCategory + $"Cannot load schedule '{file}': negative or zero repeat interval.");
                    continue;
                }

                schedule.FilePath = file;
                _log.Information(LogCategory + $"Loaded build schedule '{file}'.");
                _schedules.Add(schedule);
            }

            _client.Ready += (client, args) =>
            {
                _backgroundScheduleSource = new CancellationTokenSource();
                _backgroundScheduleTask = Task.Run(async () => await RunBuildSchedulesAsync(),
                    _backgroundScheduleSource.Token);
                _log.Information(LogCategory + "Started background build schedule task.");
                return Task.CompletedTask;
            };
        }
        
        private async Task RunBuildSchedulesAsync()
        {
            _queuedBuilds = new Queue<BuildSchedule>();
            
            while (!_backgroundScheduleSource.IsCancellationRequested)
            {
                foreach (var schedule in _schedules)
                {
                    schedule.NextRunDate ??= schedule.StartDate;
                    if (DateTimeOffset.UtcNow >= schedule.NextRunDate)
                    {
                        var span = (DateTimeOffset.UtcNow - schedule.NextRunDate).Value;
                        var amount = Math.Ceiling((double)span.Ticks / schedule.RepeatInterval.Ticks);
                        amount = Math.Max(amount, 1);
                        var newSpan = schedule.RepeatInterval * amount;
                        
                        schedule.NextRunDate += newSpan;

                        try
                        {
                            var json = JsonConvert.SerializeObject(schedule, Formatting.Indented);
                            await File.WriteAllTextAsync(schedule.FilePath, json);
                            _log.Information(LogCategory + $"Successfully updated schedule '{schedule.Name}'");
                        }
                        catch (Exception e)
                        {
                            _log.Warning(LogCategory + $"Failed to update schedule '{schedule.Name}': {e.Message}");
                        }
                        
                        if (!_queuedBuilds.Contains(schedule))
                        {
                            _queuedBuilds.Enqueue(schedule);
                        }
                    }
                }
                
                if (!_buildService.IsBuilding())
                {
                    if (_queuedBuilds.Count > 0)
                    {
                        var schedule = _queuedBuilds.Peek();
                        var configs = _buildService.GetBuildConfigurations();
                        var config = configs
                            .FirstOrDefault(c => c.Name.ToLower() == schedule.ConfigurationName.ToLower());

                        if (config == null)
                        {
                            _log.Warning(LogCategory + $"Cannot run schedule '{schedule.Name}': unknown build configuration '{schedule.ConfigurationName}'");
                        }
                        else
                        {
                            try
                            {
                                if (_buildService.StartBuild(config, _client.CurrentUser, out string errorMessage))
                                {
                                    _log.Information(LogCategory + $"Successfully ran schedule '{schedule.Name}'.");
                                    _queuedBuilds.Dequeue();
                                }
                                else
                                {
                                    _log.Warning(LogCategory + $"Failed to run schedule '{schedule.Name}': {errorMessage}");
                                    _log.Warning(LogCategory + "Another attempt will be made.");
                                }
                            }
                            catch (Exception ex)
                            {
                                _log.Error(LogCategory + $"An error has occurred while attempting to start the scheduled build: {ex.Message}. Retrying...");
                            }
                        }
                    }
                }
                
                await Task.Delay(_config.BuildScheduleCheckIntervalMilliseconds);
            }
        }
    }
}