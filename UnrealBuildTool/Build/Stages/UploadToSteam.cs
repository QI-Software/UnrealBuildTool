using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using UnrealBuildTool.Services;
using UnrealBuildTool.Services.Models;

namespace UnrealBuildTool.Build.Stages
{
    public class UploadToSteam : BuildStage
    {
        private SteamAuthService _steamAuth;
        private SteamworksUser _user;
        private Process _steamcmdProcess;
        private Task _hangCheck;
        private bool _waitingForCode = false;
        
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

        public override Task<StageResult> DoTaskAsync(IServiceProvider services)
        {
            TryGetConfigValue("SteamCMDPath", out string steamcmdPath);
            TryGetConfigValue("Username", out string username);
            TryGetConfigValue("Password", out string password);
            TryGetConfigValue("AppVDFPath", out string appVDFPath);
            TryGetConfigValue("MaxTries", out int maxTries);
            TryGetConfigValue("IsCritical", out bool isCritical);

            _steamAuth = services.GetRequiredService<SteamAuthService>();
            
            if (!File.Exists(appVDFPath))
            {
                FailureReason = $"Cannot find App VDF at '{appVDFPath}'.";
                return Task.FromResult(isCritical ? StageResult.Failed : StageResult.SuccessfulWithWarnings);
            }

            _steamAuth.TryGetAccount(username, out _user);
            
            // We'll use the SteamAuthService to get a user account if a password isn't specified.
            if (string.IsNullOrWhiteSpace(password))
            {
                if (_user == null)
                {
                    FailureReason = "You must set a username with an account using the SteamAuthService if you do not set a password.";
                    LogBuilder.AppendLine("You must set a username with an account using the SteamAuthService if you do not set a password.");
                    return Task.FromResult(StageResult.Failed);
                }
                
                OnConsoleOut($"UBT: Found user '{_user.Username}' in SteamAuthService.");
                LogBuilder.AppendLine($"UBT: Found user '{_user.Username}' in SteamAuthService.");
                password = _user.Password;
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
                        Arguments = string.Join(' ', arguments),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                    }
                };

                FailureReason = null;
                _steamcmdProcess.Start();
                _steamcmdProcess.BeginErrorReadLine();
                _steamcmdProcess.BeginOutputReadLine();
                
                _ = ConsumeReader(_steamcmdProcess.StandardOutput);
                _ = ConsumeError(_steamcmdProcess.StandardError);
                _ = WaitForSteamAuth(_steamcmdProcess);
                
                _steamcmdProcess.WaitForExit();

                if (IsCancelled)
                {
                    return Task.FromResult(StageResult.Failed);
                }
                
                // Once exited, check if the FailureReason isn't nuLL.
                if (FailureReason != null)
                {
                    continue;
                }

                switch (_steamcmdProcess.ExitCode)
                {
                    case 0: return Task.FromResult(StageResult.Successful);
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

            return Task.FromResult(isCritical ? StageResult.Failed : StageResult.SuccessfulWithWarnings);
        }

        public override Task OnCancellationRequestedAsync()
        {
            base.OnCancellationRequestedAsync();
            
            _steamcmdProcess.Kill(true);
            return Task.CompletedTask;
        }

        private string _currentOutput = "";
        async Task ConsumeReader(TextReader reader)
        {
            char[] buffer = new char[1];

            while ((await reader.ReadAsync(buffer, 0, 1)) > 0)
            {
                Console.Write(buffer[0]);
                // if (buffer[0].Equals('\n') )
                // {
                //     OnConsoleOut(_currentOutput);
                //     _currentOutput = "";
                //     continue;
                // }
                //
                // _currentOutput += buffer[0];
                // if (_currentOutput.Contains("Two-factor code:"))
                // {
                //     OnConsoleOut(_currentOutput);
                //     _currentOutput = "";
                //     _waitingForCode = true;
                // }
                //
                // if (_currentOutput.Length >= 128)
                // {
                //     OnConsoleOut(_currentOutput);
                //     _currentOutput = "";
                // }
            }
        }

        private string _currentError = "";
        async Task ConsumeError(TextReader reader)
        {
            char[] buffer = new char[1];

            while ((await reader.ReadAsync(buffer, 0, 1)) > 0)
            {
                Console.Write(buffer[0]);
                // if (buffer[0].Equals('\n') )
                // {
                //     OnConsoleOut(_currentError);
                //     _currentError = "";
                //     continue;
                // }
                //
                // _currentError += buffer[0];
                // if (_currentError.Contains("Two-factor code:"))
                // {
                //     OnConsoleOut(_currentError);
                //     _currentError = "";
                //     _waitingForCode = true;
                // }
                //
                // if (_currentError.Length >= 128)
                // {
                //     OnConsoleOut(_currentError);
                //     _currentError = "";
                // }
            }
        }

        async Task WaitForSteamAuth(Process steamcmd)
        {
            while (!steamcmd.HasExited)
            {
                if (!_waitingForCode)
                {
                    await Task.Delay(500);
                }
                else
                {
                    OnConsoleOut("UBT: SteamCMD is hanging! Probably waiting for a Steam Guard code.");
                    LogBuilder.AppendLine("UBT: SteamCMD is hanging! Probably waiting for a Steam Guard code.");

                    if (_user == null)
                    {
                        OnConsoleOut("UBT: No SteamAuth user found, cannot get Steam Guard code. Failing.");
                        LogBuilder.AppendLine("UBT: No SteamAuth user found, cannot get Steam Guard code. Failing.");

                        await OnCancellationRequestedAsync();
                        return;
                    }

                    if (_steamAuth.GetCodeForAccount(_user.Username, out string code, out string error))
                    {
                        OnConsoleOut($"UBT: Retrieved Steam Guard code '{code}', feeding to SteamCMD.");
                        LogBuilder.AppendLine($"UBT: Retrieved Steam Guard code '{code}', feeding to SteamCMD.");
                        await steamcmd.StandardInput.WriteLineAsync(code);
                        _waitingForCode = false;
                        return;
                    }

                    OnConsoleOut($"UBT: Failed to retrieve code: {error}.");
                    LogBuilder.AppendLine($"UBT: Failed to retrieve code: {error}.");
                    await OnCancellationRequestedAsync();
                    return;
                }
            }
        }
    }
}