using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class UploadToSteam : BuildStage
    {
        private Process _steamcmdProcess;
        private Task _hangCheck;
        private bool _hanging = false;
        
        public override string GetName() => nameof(UploadToSteam);

        public override string GetDescription()
        {
            TryGetConfigValue("FriendlyName", out string friendlyName);
            return $"Upload app '{friendlyName}' to Steam.";
        }

        public override bool IsStageConfigurationValid(out string ErrorMessage)
        {
            if (!base.IsStageConfigurationValid(out ErrorMessage))
            {
                return false;
            }

            TryGetConfigValue("SteamCMDPath", out string path);
            TryGetConfigValue("Username", out string user);

            if (!File.Exists(path))
            {
                ErrorMessage = $"Could not find SteamCMD at path '{path}'";
                return false;
            }

            if (string.IsNullOrEmpty(user))
            {
                ErrorMessage = $"Cannot upload to Steam with null or empty username.";
                return false;
            }
            
            return true;
        }

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("SteamCMDPath", "/Path/To/SteamCMD.exe");
            AddDefaultConfigurationKey("Username", "");
            AddDefaultConfigurationKey("Password", "");
            AddDefaultConfigurationKey("AppVDFPath", "");
            AddDefaultConfigurationKey("FriendlyName", "");
            AddDefaultConfigurationKey("MaxTries", 2);
            AddDefaultConfigurationKey("IsCritical", false);
            AddDefaultConfigurationKey("RunInBackground", true);
        }

        public override bool RunInBackground()
        {
            TryGetConfigValue("RunInBackground", out bool runInBackground);
            return runInBackground;
        }

        public override List<BuildStage> GetIncompatibleBackgroundStages(List<BuildStage> stages)
        {
            if (RunInBackground())
            {
                return stages.Where(s => s.GetType() == typeof(UploadToSteam)).ToList();
            }

            return new List<BuildStage>();
        }

        public override async Task<StageResult> DoTaskAsync()
        {
            TryGetConfigValue("SteamCMDPath", out string steamcmdPath);
            TryGetConfigValue("Username", out string username);
            TryGetConfigValue("Password", out string password);
            TryGetConfigValue("AppVDFPath", out string appVDFPath);
            TryGetConfigValue("MaxTries", out int maxTries);
            TryGetConfigValue("IsCritical", out bool isCritical);
            
            if (!File.Exists(appVDFPath))
            {
                FailureReason = $"Cannot find App VDF at '{appVDFPath}'.";
                return isCritical ? StageResult.Failed : StageResult.SuccessfulWithWarnings;
            }

            var arguments = new[]
            {
                string.IsNullOrWhiteSpace(password) 
                    ? $"+login {username}"
                    : $"+login {username} {password}",
                $"+run_app_build \"{appVDFPath}\"",
                $"+quit"
            };
            
            var redactedArgument = new[]
            {
                string.IsNullOrWhiteSpace(password) 
                    ? $"+login {username}"
                    : $"+login {username} [PASSWORD REDACTED]",
                $"+run_app_build \"{appVDFPath}\"",
                $"+quit"
            };
            
            OnConsoleOut($"UBT: Running SteamCMD Upload with arguments '{string.Join(' ', redactedArgument)}'");
            OnConsoleOut($"UBT: Max tries: {maxTries}");
            
            while (maxTries > 0)
            {
                maxTries--;
                
                 _steamcmdProcess = new Process
                {
                    StartInfo =
                    {
                        FileName = steamcmdPath,
                        UseShellExecute = true,
                        CreateNoWindow = false,
                    }
                };

                FailureReason = null;
                _steamcmdProcess.OutputDataReceived += (sender, args) => OnConsoleOut(args.Data);
                _steamcmdProcess.ErrorDataReceived += (sender, args) => OnConsoleError(args.Data);
                _steamcmdProcess.Start();
                _steamcmdProcess.BeginOutputReadLine();
                _steamcmdProcess.BeginErrorReadLine();

                // Verify that SteamCMD doesn't wait for input. If it does, murder it.
                _hangCheck = Task.Run(async () =>
                {
                    while (true)
                    {
                        await Task.Delay(1000);
                    
                        if (_steamcmdProcess == null || _steamcmdProcess.HasExited)
                        {
                            return;
                        }

                        bool bHangingNow = false;
                        foreach (ProcessThread pThread in _steamcmdProcess.Threads)
                        {
                            if (pThread.WaitReason == ThreadWaitReason.UserRequest)
                            {
                                OnConsoleError($"UBT: SteamCMD thread {pThread.Id} is hanging!");
                                bHangingNow = true;
                                _hanging = true;
                                break;
                            }
                        }

                        if (!bHangingNow && _hanging)
                        {
                            OnConsoleOut("UBT: SteamCMD is no longer hanging.");
                            _hanging = false;
                        }
                    }
                });

                while (!_steamcmdProcess.HasExited)
                {
                    await Task.Delay(1000);
                    
                    OnConsoleOut("UBT: SteamCMD is still active.");
                }

                if (IsCancelled)
                {
                    return StageResult.Failed;
                }
                
                // Once exited, check if the FailureReason isn't nuLL.
                if (FailureReason != null)
                {
                    continue;
                }

                switch (_steamcmdProcess.ExitCode)
                {
                    case 0: return StageResult.Successful;
                    case 3:
                        FailureReason = "Failed to load steamclient.dll";
                        break;
                    case 4:
                        FailureReason = "Invalid input parameter(s) specified.";
                        break;
                    case 5:
                        FailureReason = "Failed to login to Steam.";
                        break;
                    case 6:
                        FailureReason = "Uploading app content failed.";
                        break;
                    case 7:
                        FailureReason = "Command runscript failed.";
                        break;
                    case 8:
                        FailureReason = "Downloading app content failed.";
                        break;
                    case 9:
                        FailureReason = "Uploading workshop content failed.";
                        break;
                    case 10:
                        FailureReason = "Downloading workshop content failed.";
                        break;
                    default:
                        FailureReason = $"An unknown exit code was given ({_steamcmdProcess.ExitCode})";
                        break;
                }
            }

            return isCritical ? StageResult.Failed : StageResult.SuccessfulWithWarnings;
        }

        public override Task OnCancellationRequestedAsync()
        {
            base.OnCancellationRequestedAsync();
            
            _steamcmdProcess.Kill(true);
            return Task.CompletedTask;
        }
    }
}