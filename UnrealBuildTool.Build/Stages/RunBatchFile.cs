using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class RunBatchFile : BuildStage
    {
        private Process _batchProcess;
        public override string GetName() => "RunBatchFile";

        public override string GetDescription()
        {
            TryGetConfigValue<string>("Description", out var desc);
            return desc;
        }

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("BatchFile", typeof(string), "YourFile.bat");
            AddDefaultConfigurationKey("Arguments", typeof(string), "");
            AddDefaultConfigurationKey("Description", typeof(string), "Run console command.");
        }

        public override Task<StageResult> DoTaskAsync()
        {
            TryGetConfigValue<string>("BatchFile", out var batchFile);
            TryGetConfigValue<string>("Arguments", out var arguments);

            OnConsoleOut("UBT: Running command: " + batchFile);
            
            _batchProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = batchFile,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            };
            
            _batchProcess.ErrorDataReceived += (sender, args) => OnConsoleError(args.Data);

            try
            {
                _batchProcess.Start();
                _batchProcess.BeginOutputReadLine();
                _batchProcess.BeginErrorReadLine();
                _batchProcess.WaitForExit();
            }
            catch (Exception e)
            {
                FailureReason = "An exception occurred while running the batch file: " + e.Message;
                return Task.FromResult(StageResult.Failed);
            }

            if (IsCancelled)
            {
                return Task.FromResult(StageResult.Failed);
            }

            if (_batchProcess.ExitCode != 0)
            {
                FailureReason = $"Batch file failed with exit code {_batchProcess.ExitCode}";
                return Task.FromResult(StageResult.Failed);
            }

            return Task.FromResult(StageResult.Successful);
        }

        public override Task OnCancellationRequestedAsync()
        {
            base.OnCancellationRequestedAsync();

            if (_batchProcess != null && !_batchProcess.HasExited)
            {
                _batchProcess.Kill(true);
            }
            
            return Task.CompletedTask;
        }
    }
}