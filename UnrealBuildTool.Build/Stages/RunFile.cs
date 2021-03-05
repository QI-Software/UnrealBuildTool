﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class RunFile : BuildStage
    {
        private Process _fileProcess;
        public override string GetName() => "RunFile";

        public override string GetDescription()
        {
            TryGetConfigValue<string>("Description", out var desc);
            return desc;
        }

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("File", typeof(string), "YourFile.exe");
            AddDefaultConfigurationKey("Arguments", typeof(string), "");
            AddDefaultConfigurationKey("Description", typeof(string), "Run console command.");
        }

        public override Task<StageResult> DoTaskAsync()
        {
            TryGetConfigValue<string>("File", out var file);
            TryGetConfigValue<string>("Arguments", out var arguments);

            OnConsoleOut($"UBT: Running file with arguments: '{file} {arguments}'");
            if (!File.Exists(file))
            {
                FailureReason = $"Could not find file '{file}'";
                return Task.FromResult(StageResult.Failed);
            }
            
            _fileProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            };
            
            _fileProcess.ErrorDataReceived += (sender, args) => OnConsoleError(args.Data);

            try
            {
                _fileProcess.Start();
                _fileProcess.BeginOutputReadLine();
                _fileProcess.BeginErrorReadLine();
                _fileProcess.WaitForExit();
            }
            catch (Exception e)
            {
                FailureReason = "An exception occurred while running the file: " + e.Message;
                return Task.FromResult(StageResult.Failed);
            }

            if (IsCancelled)
            {
                return Task.FromResult(StageResult.Failed);
            }

            if (_fileProcess.ExitCode != 0)
            {
                FailureReason = $"Process failed with exit code {_fileProcess.ExitCode}";
                return Task.FromResult(StageResult.Failed);
            }

            return Task.FromResult(StageResult.Successful);
        }

        public override Task OnCancellationRequestedAsync()
        {
            base.OnCancellationRequestedAsync();

            if (_fileProcess != null && !_fileProcess.HasExited)
            {
                _fileProcess.Kill(true);
            }
            
            return Task.CompletedTask;
        }
    }
}