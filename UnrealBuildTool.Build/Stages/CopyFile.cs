using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class CopyFile : BuildStage
    {
        public override string GetName() => nameof(CopyFile);

        public override string GetDescription()
        {
            TryGetConfigValue<string>("TargetFile", out var target);
            TryGetConfigValue<string>("DestinationFile", out var destination);

            target = target.Replace(@"\", "/").Replace("//", "/").TrimEnd('/');
            destination = destination.Replace(@"\", "/").Replace("//", "/").TrimEnd('/');

            if (target.Contains("/"))
            {
                target = target.Split('/').Last();
            }
            
            if (destination.Contains("/"))
            {
                var splitDest = destination.Split("/");
                destination = splitDest.Take(splitDest.Length - 1).Aggregate((curr, next) => $"{curr}/{next}");
            }

            return $"Copy file '{target}' to '{destination}'";
        }

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("TargetFile",  "");
            AddDefaultConfigurationKey("DestinationFile",  "");
            AddDefaultConfigurationKey("ShouldOverwrite", true);
            AddDefaultConfigurationKey("IsCritical",true);
        }

        public override Task<StageResult> DoTaskAsync()
        {
            TryGetConfigValue<string>("TargetFile", out var target);
            TryGetConfigValue<string>("DestinationFile", out var destination);
            TryGetConfigValue<bool>("ShouldOverwrite", out var shouldOverwrite);
            TryGetConfigValue<bool>("IsCritical", out var critical);

            target = target.Replace(@"\", "/").Replace("//", "/").TrimEnd('/');
            destination = destination.Replace(@"\", "/").Replace("//", "/").TrimEnd('/');

            if (destination.Contains("/"))
            {
                var splitDest = destination.Split("/");
                var directory = splitDest.Take(splitDest.Length - 1).Aggregate((curr, next) => curr + next);

                if (!Directory.Exists(directory))
                {
                    try
                    {
                        Directory.CreateDirectory(directory);
                    }
                    catch (Exception)
                    {
                        OnConsoleError($"UBT: Failed to create destination directory '{directory}'");
                        TryGetConfigValue<bool>("IsCritical", out var crit);
                        if (crit)
                        {
                            FailureReason = $"UBT: Failed to create destination directory '{directory}'";
                            return Task.FromResult(critical ? StageResult.Failed : StageResult.SuccessfulWithWarnings);
                        }

                        return Task.FromResult(StageResult.SuccessfulWithWarnings);
                    }
                }
            }
            if (!File.Exists(target))
            {
                FailureReason = $"Could not find target file '{target}'";
                return Task.FromResult(critical ? StageResult.Failed : StageResult.SuccessfulWithWarnings);
            }

            OnConsoleOut($"UBT: Copying file '{target}' to '{destination}'.");

            if (File.Exists(destination) && !shouldOverwrite)
            {
                OnConsoleOut("UBT: Destination file already exists, aborting (ShouldOverwrite is false)");
                return Task.FromResult(StageResult.SuccessfulWithWarnings);
            }

            try
            {
                File.Copy(target, destination, true);
                return Task.FromResult(StageResult.Successful);

            }
            catch (Exception e)
            {
                FailureReason = $"UBT: Failed to copy '{target}' to '{destination}': {e.Message}";
                OnConsoleError(FailureReason);
                return Task.FromResult(critical ? StageResult.Failed : StageResult.SuccessfulWithWarnings);
            }
        }

        public override bool IsStageConfigurationValid(out string ErrorMessage)
        {
            if (!base.IsStageConfigurationValid(out ErrorMessage))
            {
                return false;
            }

            TryGetConfigValue<string>("TargetFile", out var target);
            TryGetConfigValue<string>("DestinationFile", out var dest);

            if (string.IsNullOrWhiteSpace(target))
            {
                ErrorMessage = "TargetFile cannot be null or empty.";
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(dest))
            {
                ErrorMessage = "DestinationFile cannot be null or empty.";
                return false;
            }

            return true;
        }
    }
}