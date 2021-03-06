using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class RunConsoleCommand : BuildStage
    {
        private Process _cmdProcess;
        public override string GetName() => "RunConsoleCommand";

        public override string GetDescription()
        {
            TryGetConfigValue("Description", out string desc);
            return desc;
        }

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("Command", "echo \"Don't forget to set a command!\"");
            AddDefaultConfigurationKey("Description", "Run console command.");
        }

        public override Task<StageResult> DoTaskAsync()
        {
            TryGetConfigValue("Command", out string cmd);
            OnConsoleOut("UBT: Running command: " + cmd);
            
            _cmdProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/C " + cmd,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            };
            
            _cmdProcess.ErrorDataReceived += (sender, args) => OnConsoleError(args.Data);

            try
            {
                _cmdProcess.Start();
                _cmdProcess.BeginOutputReadLine();
                _cmdProcess.BeginErrorReadLine();
                _cmdProcess.WaitForExit();
            }
            catch (Exception e)
            {
                FailureReason = "An exception occurred while running the command: " + e.Message;
                return Task.FromResult(StageResult.Failed);
            }

            if (IsCancelled)
            {
                return Task.FromResult(StageResult.Failed);
            }

            if (_cmdProcess.ExitCode != 0)
            {
                FailureReason = $"CMD failed with exit code {_cmdProcess.ExitCode}";
                return Task.FromResult(StageResult.Failed);
            }

            return Task.FromResult(StageResult.Successful);
        }

        public override Task OnCancellationRequestedAsync()
        {
            base.OnCancellationRequestedAsync();

            if (_cmdProcess != null && !_cmdProcess.HasExited)
            {
                _cmdProcess.Kill(true);
            }
            
            return Task.CompletedTask;
        }
    }
}