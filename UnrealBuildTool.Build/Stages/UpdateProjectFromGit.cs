using System.Diagnostics;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class UpdateProjectFromGit : BuildStage
    {
        public override string GetName() => "UpdateProjectFromGit";
    
        public override string GetDescription() => "Update project from Git";

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("RunGitClean", typeof(bool), true);
            AddDefaultConfigurationKey("RunGitResetHard", typeof(bool), true);
        }

        public override Task<StageResult> DoTaskAsync()
        {
            TryGetConfigValue<bool>("RunGitClean", out var bRunGitClean);
            TryGetConfigValue<bool>("RunGitResetHard", out var bRunGitReset);

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
                "--hard"
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
                var cleanProcess = new Process();
                cleanProcess.OutputDataReceived += (sender, args) => OnConsoleOut(args.Data);
                cleanProcess.ErrorDataReceived += (sender, args) => OnConsoleError(args.Data);
                cleanProcess.StartInfo = startInfo;
                cleanProcess.Start();
                cleanProcess.BeginOutputReadLine();
                cleanProcess.BeginErrorReadLine();
                cleanProcess.WaitForExit();

                if (cleanProcess.ExitCode != 0)
                {
                    FailureReason = $"An error occured while running git clean ({cleanProcess.ExitCode})";
                    return Task.FromResult(StageResult.Failed);
                }
            }
            
            if (bRunGitReset)
            {
                OnConsoleOut("UBT: Running git reset.");
                var resetProcess = new Process() {StartInfo = startInfo};
                resetProcess.StartInfo.Arguments = "/C " + string.Join(' ', resetArguments);
                resetProcess.OutputDataReceived += (sender, args) => OnConsoleOut(args.Data);
                resetProcess.ErrorDataReceived += (sender, args) => OnConsoleError(args.Data);
                resetProcess.Start();
                resetProcess.BeginOutputReadLine();
                resetProcess.BeginErrorReadLine();
                resetProcess.WaitForExit();

                if (resetProcess.ExitCode != 0)
                {
                    FailureReason = $"An error occured while running git reset --hard ({resetProcess.ExitCode})";
                    return Task.FromResult(StageResult.Failed);
                }
            }

            OnConsoleOut("UBT: Running git fetch.");
            var fetchProcess = new Process {StartInfo = startInfo};
            fetchProcess.StartInfo.Arguments = "/C " + string.Join(' ', fetchArguments);
            fetchProcess.OutputDataReceived += (sender, args) => OnConsoleOut(args.Data);
            fetchProcess.ErrorDataReceived += (sender, args) => OnConsoleError(args.Data);
            fetchProcess.Start();
            fetchProcess.BeginOutputReadLine();
            fetchProcess.BeginErrorReadLine();
            fetchProcess.WaitForExit();

            if (fetchProcess.ExitCode != 0)
            {
                FailureReason = $"An error has occured while running git fetch ({fetchProcess.ExitCode})";
                return Task.FromResult(StageResult.Failed);
            }
            
            OnConsoleOut("UBT: Running git pull.");
            var pullProcess = new Process {StartInfo = startInfo};
            pullProcess.StartInfo.Arguments = "/C " + string.Join(' ', pullArguments);
            pullProcess.OutputDataReceived += (sender, args) => OnConsoleOut(args.Data);
            pullProcess.ErrorDataReceived += (sender, args) => OnConsoleError(args.Data);
            pullProcess.Start();
            pullProcess.BeginOutputReadLine();
            pullProcess.BeginErrorReadLine();
            pullProcess.WaitForExit();
            
            if (pullProcess.ExitCode != 0)
            {
                FailureReason = $"An error has occured while running git pull ({pullProcess.ExitCode})";
                return Task.FromResult(StageResult.Failed);
            }

            return Task.FromResult(StageResult.Successful);
        }
    }
}