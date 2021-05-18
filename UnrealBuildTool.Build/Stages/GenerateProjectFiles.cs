using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnrealBuildTool.Build.Stages
{
    public class GenerateProjectFiles : BuildStage
    {
        public override string GetName() => "GenerateProjectFiles";

        public override string GetDescription() => "Verify .uproject and Generate Project Files";

        public override async Task<StageResult> DoTaskAsync()
        {
            // Make sure our uproject is associated correctly.
            var uprojectPath = BuildConfig.GetProjectFilePath();
            if (!File.Exists(uprojectPath))
            {
                FailureReason = $"Could not find .uproject file at {uprojectPath}.";
                return StageResult.Failed;
            }
            
            JObject uProject;
            try
            {
                var json = await File.ReadAllTextAsync(uprojectPath);
                uProject = JsonConvert.DeserializeObject<JObject>(json);
            }
            catch (Exception e)
            {
                FailureReason = "Failed to read .uproject file: " + e.Message;
                return StageResult.Failed;
            }

            var currentAssociation = uProject["EngineAssociation"];
            if (string.IsNullOrWhiteSpace(currentAssociation?.Value<string>()))
            {
                FailureReason = "Could not read key 'EngineAssociation' from .uproject file.";
                return StageResult.Failed;
            }

            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Epic Games\Unreal Engine\Builds"))
            {
                if (key == null)
                {
                    FailureReason = @"Could not find key Software\Epic Games\Unreal Engine\Builds";
                    return StageResult.Failed;
                }
                
                if (key.GetValue(currentAssociation.Value<string>()) == null)
                {
                    OnConsoleOut(
                        "UBT: Warning, .uproject file not associated to local engine. Please make sure not to push .uproject files in your git commits.");
                    string validAssociation = key
                        .GetValueNames()
                        .FirstOrDefault(s => ((string)key.GetValue(s))?.TrimEnd('/') == BuildConfig.EngineDirectory.TrimEnd('/').Replace(@"\", "/"));

                    OnConsoleOut("UBT: Listing local associations available.");
                    foreach (var valueName in key.GetValueNames())
                    {
                        OnConsoleOut($"{valueName} | {key.GetValue(valueName) as string ?? "NULL"}");
                    }
                    
                    if (validAssociation == null)
                    {
                        FailureReason =
                            "Local machine has no engine association with the same path as the BuildConfig EngineDirectory, please run association manually at least once, or fix the configuration.";
                        return StageResult.Failed;
                    }

                    uProject.Property("EngineAssociation")?.Remove();
                    uProject.Property("FileVersion")?.AddAfterSelf(new JProperty("EngineAssociation", validAssociation));
                    var json = JsonConvert.SerializeObject(uProject, Formatting.Indented);
                    await File.WriteAllTextAsync(uprojectPath, json);

                    OnConsoleOut(
                        $"UBT: Reassociated uproject file to engine {validAssociation} at '{key.GetValue(validAssociation)}'.");
                }
            }
            
            // Re-generate intermediate and the solution file.
            var UBTPath = BuildConfig.GetUnrealBuildToolPath();

            var arguments = new[]
            {
                "-projectfiles",
                $"-project=\"{BuildConfig.GetProjectFilePath()}\"",
                "-game",
                "-rocket",
                "-progress"
            };

            var process = new Process();
            var startInfo = new ProcessStartInfo
            {
                FileName = UBTPath,
                Arguments = string.Join(" ", arguments),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            process.StartInfo = startInfo;
            process.OutputDataReceived += (sender, args) =>
            {
                OnConsoleOut(args.Data);
                LogWriter.Write(args.Data);
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                Console.WriteLine(args.Data);
                LogWriter.Write(args.Data);
                OnConsoleError(args.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                return StageResult.Successful;
            }

            FailureReason = "An error has occured while generating project files.";
            return StageResult.Failed;
        }
    }
}