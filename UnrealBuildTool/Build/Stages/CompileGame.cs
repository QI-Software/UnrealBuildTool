using System;
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
            TryGetConfigValue("GameConfiguration", out string config);
            TryGetConfigValue("GamePlatform", out string platform);
            TryGetConfigValue("GameTarget", out string target);
            TryGetConfigValue("CompileGame", out bool compileGame);

            if (compileGame)
            {
                return $"Compile '{target}' with configuration [{config} | {platform}]";
            }
            
            return $"Compile '{target}' with configuration 'Development Editor'";
        }

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("GameConfiguration", "Shipping");
            AddDefaultConfigurationKey("GamePlatform", "Win64");
            AddDefaultConfigurationKey("GameTarget", "TargetProject");
            AddDefaultConfigurationKey("CompileEditor", true);
            AddDefaultConfigurationKey("CompileGame", true);
            AddDefaultConfigurationKey("MSBuildPath", "MSBuild.exe");
        }

        public override Task<StageResult> DoTaskAsync(IServiceProvider services)
        {
            TryGetConfigValue("GameConfiguration", out string config);
            TryGetConfigValue("GamePlatform", out string platform);
            TryGetConfigValue("GameTarget", out string target);
            TryGetConfigValue("MSBuildPath", out string msbuildPath);
            TryGetConfigValue("CompileEditor", out bool compileEditor);
            TryGetConfigValue("CompileGame", out bool compileGame);

            if (compileEditor)
            {
                var msbuildArguments = new[]
                {
                    $"\"{BuildConfig.GetSolutionFilePath()}\"",
                    "-p:Configuration=\"Development Editor\"",
                    $"/property:Platform=\"{platform}\"",
                };

                OnConsoleOut($"UBT: Running MSBuild.exe with arguments: '{string.Join(' ', msbuildArguments)}'");

                _msbuildProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = msbuildPath,
                        Arguments = string.Join(' ', msbuildArguments),
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
            }
            
            //var manifestPath = $"{BuildConfig.EngineDirectory}/Engine/Intermediate/Build/Manifest.xml";
            //manifestPath = manifestPath.Replace("//", "/").Replace(@"\", "/");

            if (compileGame)
            {
                var ubtPath = BuildConfig.GetUnrealBuildToolPath();
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
                OnConsoleOut($"UBT: Running compile with command line: '{_ubtProcess.StartInfo.Arguments}'");
                
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
            }
            
            return Task.FromResult(StageResult.Successful);
        }

        public override Task OnCancellationRequestedAsync()
        {
            base.OnCancellationRequestedAsync();

            if (_msbuildProcess != null && !_msbuildProcess.HasExited)
            {
                _msbuildProcess.Kill(true);
            }
            
            if (_ubtProcess != null && !_ubtProcess.HasExited)
            {
                _ubtProcess.Kill(true);
            }
            
            return Task.CompletedTask;
        }
        
        public override bool IsStageConfigurationValid(out string ErrorMessage)
        {
            if (!base.IsStageConfigurationValid(out ErrorMessage))
            {
                return false;
            }
            
            TryGetConfigValue("MSBuildPath", out string msbuildPath);
            TryGetConfigValue("CompileEditor", out bool compileEditor);
            TryGetConfigValue("CompileGame", out bool compileGame);

            if (!compileEditor && !compileGame)
            {
                ErrorMessage = "CompileEditor and CompileGame cannot both be false.";
                return false;
            }
            
            if (compileEditor && !File.Exists(msbuildPath))
            {
                ErrorMessage = $"Could not locate MSBuild.exe at '{msbuildPath}'.";
                return false;
            }

            ErrorMessage = null;
            return true;
        }
    }
}