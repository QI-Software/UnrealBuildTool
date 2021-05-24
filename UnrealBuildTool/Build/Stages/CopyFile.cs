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
            TryGetConfigValue("TargetFile", out string target);
            TryGetConfigValue("DestinationFile", out string destination);

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

        public override Task<StageResult> DoTaskAsync(IServiceProvider services)
        {
            TryGetConfigValue("TargetFile", out string target);
            TryGetConfigValue("DestinationFile", out string destination);
            TryGetConfigValue("ShouldOverwrite", out bool shouldOverwrite);
            TryGetConfigValue("IsCritical", out bool critical);

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
                        if (critical)
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
                OnConsoleOut($"UBT: {FailureReason}");
                return Task.FromResult(critical ? StageResult.Failed : StageResult.SuccessfulWithWarnings);
            }

            OnConsoleOut($"UBT: Copying file '{target}' to '{destination}'.");

            if (File.Exists(destination) && !shouldOverwrite)
            {
                OnConsoleOut("UBT: Destination file already exists, aborting (ShouldOverwrite is false)");
                return Task.FromResult(StageResult.SuccessfulWithWarnings);
            }
            
            if (destination.Contains("/"))
            {
                var splits = destination.Split('/');
                var parentDir = splits.Take(splits.Length - 1).Aggregate((curr, next) => $"{curr}/{next}");
                if (!Directory.Exists(parentDir))
                {
                    OnConsoleOut($"UBT: Parent destination directory '{parentDir}' does not exist, creating.");

                    try
                    {
                        Directory.CreateDirectory(parentDir);
                    }
                    catch (Exception e)
                    {
                        FailureReason = $"Failed to create parent directory '{parentDir}': {e.Message}";
                        OnConsoleError(FailureReason);
                        return Task.FromResult(critical ? StageResult.Failed : StageResult.SuccessfulWithWarnings);
                    }
                }
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

            TryGetConfigValue("TargetFile", out string target);
            TryGetConfigValue("DestinationFile", out string dest);

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