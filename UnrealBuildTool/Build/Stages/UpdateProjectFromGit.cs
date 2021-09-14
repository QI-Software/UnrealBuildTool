using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class UpdateProjectFromGit : BuildStage
    {
        private Process _cleanProcess;
        private Process _resetProcess;
        private Process _fetchProcess;
        private Process _pullProcess;
        private Process _pruneProcess;

        public override string GetName() => "UpdateProjectFromGit";
    
        public override string GetDescription() => "Update project from Git";

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("RunGitRemotePruneOrigin", true);
            AddDefaultConfigurationKey("RunGitClean",  true);
            AddDefaultConfigurationKey("RunGitResetHard",  true);
            AddDefaultConfigurationKey("RunGitLFSPrune", true);
            AddDefaultConfigurationKey("MainBranchName", "master");
        }

        public override Task<StageResult> DoTaskAsync(IServiceProvider services)
        {
            TryGetConfigValue("RunGitRemotePruneOrigin", out bool bRunGitRemotePruneOrigin);
            TryGetConfigValue("RunGitClean", out bool bRunGitClean);
            TryGetConfigValue("RunGitResetHard", out bool bRunGitReset);
            TryGetConfigValue("RunGitLFSPrune", out bool bRunGitLFSPrune);
            TryGetConfigValue("MainBranchName", out string mainBranchName);

            var remotePruneArguments = new[]
            {
                "git",
                $"-C \"{BuildConfig.ProjectDirectory}\"",
                "remote",
                "prune",
                "origin"
            };
            
            var cleanArguments = new[]
            {
                "git",
                $"-C \"{BuildConfig.ProjectDirectory}\"",
                "clean",
                "-xfd"
            };

            var resetArguments = new[]
            {
                "git",
                $"-C \"{BuildConfig.ProjectDirectory}\"",
                "reset",
                "--hard",
                $"origin/{mainBranchName}"
            };

            var lfsPruneArguments = new[]
            {
                "git",
                $"-C \"{BuildConfig.ProjectDirectory}\"",
                "lfs",
                "prune"
            };

            var fetchArguments = new[]
            {
                "git",
                $"-C \"{BuildConfig.ProjectDirectory}\"",
                "fetch"
            };
            
            var pullArguments = new[]
            {
                "git",
                $"-C \"{BuildConfig.ProjectDirectory}\"",
                "pull"
            };

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C " + string.Join(' ', cleanArguments),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            if (bRunGitClean)
            {
                OnConsoleOut("UBT: Running git clean.");
                _cleanProcess = new Process();
                _cleanProcess.OutputDataReceived += (sender, args) => OnConsoleOut(args.Data);
                _cleanProcess.ErrorDataReceived += (sender, args) => OnConsoleError(args.Data);
                _cleanProcess.StartInfo = startInfo;
                _cleanProcess.Start();
                _cleanProcess.BeginOutputReadLine();
                _cleanProcess.BeginErrorReadLine();
                _cleanProcess.WaitForExit();

                if (IsCancelled)
                {
                    return Task.FromResult(StageResult.Failed);
                }

                if (_cleanProcess.ExitCode != 0)
                {
                    FailureReason = $"An error occured while running git clean ({_cleanProcess.ExitCode})";
                    return Task.FromResult(StageResult.Failed);
                }
            }
            
            if (bRunGitRemotePruneOrigin)
            {
                OnConsoleOut("UBT: Running git remote prune origin.");
                _pruneProcess = new Process() {StartInfo = startInfo};
                _pruneProcess.StartInfo.Arguments = "/C " + string.Join(' ', remotePruneArguments);
                _pruneProcess.OutputDataReceived += (sender, args) => OnConsoleOut(args.Data);
                _pruneProcess.ErrorDataReceived += (sender, args) => OnConsoleError(args.Data);
                _pruneProcess.Start();
                _pruneProcess.BeginOutputReadLine();
                _pruneProcess.BeginErrorReadLine();
                _pruneProcess.WaitForExit();

                if (IsCancelled)
                {
                    return Task.FromResult(StageResult.Failed);
                }

                if (_cleanProcess.ExitCode != 0)
                {
                    FailureReason = $"An error occured while running git remote prune origin ({_pruneProcess.ExitCode})";
                    return Task.FromResult(StageResult.Failed);
                }
            }
            
            if (bRunGitReset)
            {
                OnConsoleOut("UBT: Running git reset.");
                _resetProcess = new Process() {StartInfo = startInfo};
                _resetProcess.StartInfo.Arguments = "/C " + string.Join(' ', resetArguments);
                _resetProcess.OutputDataReceived += (sender, args) => OnConsoleOut(args.Data);
                _resetProcess.ErrorDataReceived += (sender, args) => OnConsoleError(args.Data);
                _resetProcess.Start();
                _resetProcess.BeginOutputReadLine();
                _resetProcess.BeginErrorReadLine();
                _resetProcess.WaitForExit();
                
                if (IsCancelled)
                {
                    return Task.FromResult(StageResult.Failed);
                }

                if (_resetProcess.ExitCode != 0)
                {
                    FailureReason = $"An error occured while running git reset --hard ({_resetProcess.ExitCode})";
                    return Task.FromResult(StageResult.Failed);
                }
            }
            
            if (bRunGitLFSPrune)
            {
                OnConsoleOut("UBT: Running git lfs prune.");
                _pruneProcess = new Process() {StartInfo = startInfo};
                _pruneProcess.StartInfo.Arguments = "/C " + string.Join(' ', lfsPruneArguments);
                _pruneProcess.OutputDataReceived += (sender, args) => OnConsoleOut(args.Data);
                _pruneProcess.ErrorDataReceived += (sender, args) => OnConsoleError(args.Data);
                _pruneProcess.Start();
                _pruneProcess.BeginOutputReadLine();
                _pruneProcess.BeginErrorReadLine();
                _pruneProcess.WaitForExit();
                
                if (IsCancelled)
                {
                    return Task.FromResult(StageResult.Failed);
                }

                if (_pruneProcess.ExitCode != 0)
                {
                    FailureReason = $"An error occured while running git lfs prune ({_pruneProcess.ExitCode})";
                    return Task.FromResult(StageResult.Failed);
                }
            }

            OnConsoleOut("UBT: Running git fetch.");
            _fetchProcess = new Process {StartInfo = startInfo};
            _fetchProcess.StartInfo.Arguments = "/C " + string.Join(' ', fetchArguments);
            _fetchProcess.OutputDataReceived += (sender, args) => OnConsoleOut(args.Data);
            _fetchProcess.ErrorDataReceived += (sender, args) => OnConsoleError(args.Data);
            _fetchProcess.Start();
            _fetchProcess.BeginOutputReadLine();
            _fetchProcess.BeginErrorReadLine();
            _fetchProcess.WaitForExit();
            
            if (IsCancelled)
            {
                return Task.FromResult(StageResult.Failed);
            }

            if (_fetchProcess.ExitCode != 0)
            {
                FailureReason = $"An error has occured while running git fetch ({_fetchProcess.ExitCode})";
                return Task.FromResult(StageResult.Failed);
            }
            
            OnConsoleOut("UBT: Running git pull.");
            _pullProcess = new Process {StartInfo = startInfo};
            _pullProcess.StartInfo.Arguments = "/C " + string.Join(' ', pullArguments);
            _pullProcess.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    OnConsoleOut(args.Data);
                    LogBuilder.AppendLine(args.Data);
                }
            };
            
            _pullProcess.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    OnConsoleError(args.Data);
                    LogBuilder.AppendLine(args.Data);
                }
            };
            
            _pullProcess.Start();
            _pullProcess.BeginOutputReadLine();
            _pullProcess.BeginErrorReadLine();
            _pullProcess.WaitForExit();
            
            if (IsCancelled)
            {
                return Task.FromResult(StageResult.Failed);
            }
            
            if (_pullProcess.ExitCode != 0)
            {
                FailureReason = $"An error has occured while running git pull ({_pullProcess.ExitCode})";
                return Task.FromResult(StageResult.Failed);
            }

            return Task.FromResult(StageResult.Successful);
        }

        public override Task OnCancellationRequestedAsync()
        {
            base.OnCancellationRequestedAsync();

            if (_cleanProcess != null && !_cleanProcess.HasExited)
            {
                _cleanProcess.Kill(true);
            }
            
            if (_resetProcess != null && !_resetProcess.HasExited)
            {
                _resetProcess.Kill(true);
            }
            
            if (_pruneProcess != null && !_pruneProcess.HasExited)
            {
                _pruneProcess.Kill(true);
            }
            
            if (_fetchProcess != null && !_fetchProcess.HasExited)
            {
                _fetchProcess.Kill(true);
            }
            
            if (_pullProcess != null && !_pullProcess.HasExited)
            {
                _pullProcess.Kill(true);
            }

            return Task.CompletedTask;
        }
    }
}