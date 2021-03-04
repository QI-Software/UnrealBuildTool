using System.Diagnostics;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class UpdateProjectFromGit : BuildStage
    {
        public override string GetName() => "UpdateProjectFromGit";
    
        public override string GetDescription() => "Updating project from Git";

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("RunGitClean", typeof(bool), true);
        }

        public override Task<StageResult> DoTaskAsync()
        {
            bool bRunGitClean = false;

            if (StageConfiguration["RunGitClean"] is bool b)
            {
                bRunGitClean = b;
            }

            var cleanArguments = new[]
            {
                "git",
                $"-C \"{BuildConfig.ProjectDirectory}\"",
                "clean",
                "-xfd"
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
                "fetch"
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
                    FailureReason = "An error occured while running git clean.";
                    return Task.FromResult(StageResult.Failed);
                }
            }

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
                FailureReason = "An error has occured while running git fetch.";
                return Task.FromResult(StageResult.Failed);
            }
            
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
                FailureReason = "An error has occured while running git pull.";
                return Task.FromResult(StageResult.Failed);
            }

            return Task.FromResult(StageResult.Successful);
        }
    }
}