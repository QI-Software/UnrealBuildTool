using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class ResavePackages : BuildStage
    {
        private Process _uatProcess;
        public override string GetName() => "ResavePackages";

        public override string GetDescription() => "Resave all project packages.";
        public override Task<StageResult> DoTaskAsync()
        {
            var ue4cmd = $"{BuildConfig.EngineDirectory}/Engine/Binaries/Win64/UE4Editor-Cmd.exe";
            ue4cmd = ue4cmd.Replace(@"\", "/").Replace("//", "/");

            if (!File.Exists(ue4cmd))
            {
                FailureReason = $"Could not find UE4Editor-Cmd.exe at '{ue4cmd}'";
                return Task.FromResult(StageResult.Failed);
            }
            
            var uatBat = $"{BuildConfig.EngineDirectory}/Engine/Build/BatchFiles/RunUAT.bat";
            uatBat = uatBat.Replace(@"\", "/").Replace("//", "/");

            if (!File.Exists(uatBat))
            {
                FailureReason = $"Could not find RunUAT.bat at '{uatBat}'";
                return Task.FromResult(StageResult.Failed);
            }

            var arguments = new[]
            {
                "ResavePackages",
                "-nop4",
                $"-project=\"{BuildConfig.GetProjectFilePath()}\"",
                $"-ue4exe=\"{ue4cmd}\"",
                "-CrashReporter",
                "-utf8output",
                "-GarbageCollectionFrequency=10",
                "-BuildHLOD",
                "-BuildOptions=\"Clusters, Proxies, ForceEnableHLOD\"",
                "-PackageSubstring=\"SubLevels\"",
                "-ForceHLODSetupAsset=\"DontRemoveThisTrustMe\"",
                "-logcmds=\"LogContentCommandlet all\"",
            };
            
            _uatProcess = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = uatBat,
                    Arguments = string.Join(' ', arguments),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                },
                PriorityClass = ProcessPriorityClass.High,
            };
            
            _uatProcess.OutputDataReceived += (sender, args) => OnConsoleOut(args.Data);
            _uatProcess.ErrorDataReceived += (sender, args) => OnConsoleError(args.Data);
            _uatProcess.Start();
            _uatProcess.BeginOutputReadLine();
            _uatProcess.BeginErrorReadLine();
            _uatProcess.WaitForExit();

            if (IsCancelled)
            {
                return Task.FromResult(StageResult.Failed);
            }

            if (_uatProcess.ExitCode != 0)
            {
                FailureReason = $"UAT failed with exit code {_uatProcess.ExitCode}";
                return Task.FromResult(StageResult.Failed);
            }

            return Task.FromResult(StageResult.Successful);
        }

        public override Task OnCancellationRequestedAsync()
        {
            base.OnCancellationRequestedAsync();

            if (_uatProcess != null && !_uatProcess.HasExited)
            {
                _uatProcess.Kill(true);
            }
            
            return Task.CompletedTask;
        }

        public override bool IsStageConfigurationValid(out string ErrorMessage)
        {
            if (!base.IsStageConfigurationValid(out ErrorMessage))
            {
                return false;
            }
            
            TryGetConfigValue<string>("GameConfiguration", out var gameConfig);
            
            if (!BuildConfiguration.IsValidConfiguration(gameConfig))
            {
                ErrorMessage = $"Invalid game configuration '{gameConfig}'";
                return false;
            }

            ErrorMessage = null;
            return true;
        }
    }
}