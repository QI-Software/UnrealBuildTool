using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class CompileGame : BuildStage
    {
        public override string GetName() => "CompileGame";

        private Process _ubtProcess;
        private Process _msbuildProcess;

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
            var ubtPath = BuildConfig.GetUnrealBuildToolPath();

            TryGetConfigValue<string>("GameConfiguration", out var config);
            TryGetConfigValue<string>("GamePlatform", out var platform);
            TryGetConfigValue<string>("GameTarget", out var target);

            //var manifestPath = $"{BuildConfig.EngineDirectory}/Engine/Intermediate/Build/Manifest.xml";
            //manifestPath = manifestPath.Replace("//", "/").Replace(@"\", "/");
            
            var ubtArguments = new[]
            {
                config,
                platform,
                $"-Project=\"{BuildConfig.GetProjectFilePath()}\"",
                "-TargetType=Game",
                $"\"{BuildConfig.GetProjectFilePath()}\"",
                //$"-Manifest=\"{manifestPath}\"",
                "-Progress",
                "-NoHotReloadFromIDE",
            };

            /*var ubtPath = $"{BuildConfig.EngineDirectory}/Engine/Build/BatchFiles/Build.bat"
                .Replace(@"\", "/")
                .Replace("//", "/");

            if (!File.Exists(ubtPath))
            {
                FailureReason = $"Could not find Build.bat at {ubtPath}";
                return Task.FromResult(StageResult.Failed);
            }
            
            var ubtArguments = new[]
            {
                $"-Target=\"{target} {platform} {config} -Project=\\\"{BuildConfig.GetProjectFilePath()}\\\"\"",
                //$"-Target=\"ShaderCompilerWorker {platform} Development -Quiet\"",
                "-Progress",
                "-WaitMutex",
                "-FromMsBuild",
                "-TargetType=Game",
                $"\"{BuildConfig.GetProjectFilePath()}\"",
                $"-Manifest=\"{manifestPath}\""
            };*/
            
            _ubtProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ubtPath,
                    Arguments = string.Join(" ", ubtArguments),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            };
            OnConsoleOut("UBT: Running compile with command line: " + _ubtProcess.StartInfo.Arguments);
            
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

            var msbuildArguments = new[]
            {
                "MSBuild",
                $"\"{BuildConfig.GetProjectFilePath()}\"",
                $"-p:Configuration=\"{config}\"",
                $"/property:Platform=\"{platform}\"",
                "< nul"
            };

            _msbuildProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/C " + string.Join(' ', msbuildArguments),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            };
            
            _msbuildProcess.OutputDataReceived += (sender, args) => OnConsoleOut(args.Data);
            _msbuildProcess.ErrorDataReceived += (sender, args) => OnConsoleError(args.Data);
            _msbuildProcess.Start();
            _msbuildProcess.BeginOutputReadLine();
            _msbuildProcess.BeginErrorReadLine();
            _msbuildProcess.WaitForExit();
            
            if (IsCancelled)
            {
                return Task.FromResult(StageResult.Failed);
            }

            if (_msbuildProcess.ExitCode != 0)
            {
                FailureReason = $"MSBuild.exe failed with exit code {_msbuildProcess.ExitCode}";
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

            if (_msbuildProcess != null && !_msbuildProcess.HasExited)
            {
                _msbuildProcess.Kill();
            }
            
            return Task.CompletedTask;
        }
    }
}