using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class CookGame : BuildStage
    {
        private Process _uatProcess;
        public override string GetName() => "CookGame";

        public override string GetDescription()
        {
            throw new System.NotImplementedException();
        }

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("GamePlatform", typeof(string), "Win64");
            AddDefaultConfigurationKey("GameConfiguration", typeof(string), "Shipping");
            AddDefaultConfigurationKey("GameTarget", typeof(string), "TargetProject");

        }

        public override Task<StageResult> DoTaskAsync()
        {
            TryGetConfigValue<string>("GameConfiguration", out var gameConfig);
            TryGetConfigValue<string>("GamePlatform", out var gamePlatform);
            TryGetConfigValue<string>("GameTarget", out var gameTarget);

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
                "BuildCookRun",
                "-nocompileeditor",
                "-nop4",
                $"-project=\"{BuildConfig.GetProjectFilePath()}\"",
                "-cook",
                "-stage",
                "-package",
                $"-ue4exe=\"{ue4cmd}\"",
                "-pak",
                $"-targetplatform={gamePlatform}",
                "-CrashReporter",
                "-utf8output",
                $"-target={gameTarget}",
                $"-serverconfig={gameConfig}",
                $"-clientconfig={gameConfig}",
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
                }
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

            if (_uatProcess != null && _uatProcess.HasExited)
            {
                _uatProcess.Kill();
            }
            
            return Task.CompletedTask;
        }
    }
}