using System.Diagnostics;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class CompileGame : BuildStage
    {
        public override string GetName() => "CompileGame";

        private Process _ubtProcess;

        public override string GetDescription()
        {
            TryGetConfigValue<string>("GameConfiguration", out var config);
            TryGetConfigValue<string>("GamePlatform", out var platform);
            TryGetConfigValue<string>("GameTarget", out var target);
            
            return $"Compile '{target}' with configuration [{config} | {platform}]";
        }

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("GameConfiguration", typeof(string), "Shipping");
            AddDefaultConfigurationKey("GamePlatform", typeof(string), "Win64");
            AddDefaultConfigurationKey("GameTarget", typeof(string), "TargetProject");
        }

        public override Task<StageResult> DoTaskAsync()
        {
            var UBTPath = BuildConfig.GetUnrealBuildToolPath();

            TryGetConfigValue<string>("GameConfiguration", out var config);
            TryGetConfigValue<string>("GamePlatform", out var platform);
            TryGetConfigValue<string>("GameTarget", out var target);
            
            var ubtArguments = new[]
            {
                config,
                platform,
                $"-Project=\"{BuildConfig.GetProjectFilePath()}\"",
                "-TargetType=Game",
                "-Progress",
                "-NoHotReloadFromIDE",
            };
            
            _ubtProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = UBTPath,
                    Arguments = string.Join(" ", ubtArguments),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            };

            _ubtProcess.OutputDataReceived += (sender, args) => OnConsoleOut(args.Data);
            _ubtProcess.ErrorDataReceived += (sender, args) => OnConsoleError(args.Data);
            _ubtProcess.Start();
            _ubtProcess.BeginOutputReadLine();
            _ubtProcess.BeginErrorReadLine();
            _ubtProcess.WaitForExit();

            if (IsCancelled)
            {
                return Task.FromResult(StageResult.Failed);
            }

            if (_ubtProcess.ExitCode != 0)
            {
                FailureReason = $"UnrealBuildTool.exe failed with exit code {_ubtProcess.ExitCode}";
                return Task.FromResult(StageResult.Failed);
            }

            return Task.FromResult(StageResult.Successful);
        }

        public override Task OnCancellationRequestedAsync()
        {
            base.OnCancellationRequestedAsync();
            
            if (_ubtProcess != null && !_ubtProcess.HasExited)
            {
                _ubtProcess.Kill();
            }

            return Task.CompletedTask;
        }
    }
}