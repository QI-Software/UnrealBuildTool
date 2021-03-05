using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class CookGame : BuildStage
    {
        private Process _uatProcess;
        public override string GetName() => "CookGame";

        public override string GetDescription()
        {
            TryGetConfigValue<string>("GameTarget", out var gameTarget);
            return $"Cooking game with target '{gameTarget}'";
        }

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("GamePlatform", typeof(string), "Win64");
            AddDefaultConfigurationKey("GameConfiguration", typeof(string), "Shipping");
            AddDefaultConfigurationKey("GameTarget", typeof(string), "TargetProject");
            AddDefaultConfigurationKey("IsServer", typeof(bool), false);
        }

        public override Task<StageResult> DoTaskAsync()
        {
            TryGetConfigValue<string>("GameConfiguration", out var gameConfig);
            TryGetConfigValue<string>("GamePlatform", out var gamePlatform);
            TryGetConfigValue<string>("GameTarget", out var gameTarget);
            TryGetConfigValue<bool>("IsServer", out var isServer);
            
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
                "-build",
                "-CrashReporter",
                "-utf8output",
                $"-target={gameTarget}",
                $"-{(isServer ? "serverconfig" : "clientconfig")}={gameConfig}",
                "-compile",
            };

            if (gameConfig == "Shipping" || gameConfig == "Test")
            {
                var args = arguments.ToList();
                args.Add("-prereqs");
                arguments = args.ToArray();
            }
            else
            {
                var args = arguments.ToList();
                args.Add("-compressed");
                arguments = args.ToArray();
            }

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