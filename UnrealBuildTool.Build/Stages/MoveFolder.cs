using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class MoveFolder : BuildStage
    {
        public override string GetName() => "MoveFolder";

        public override string GetDescription()
        {
            TryGetConfigValue("Target", out string target);
            TryGetConfigValue("Destination", out string destination);

            target = target.Replace(@"\", "/").Replace("//", "/").TrimEnd('/');
            destination = destination.Replace(@"\", "/").Replace("//", "/").TrimEnd('/');

            if (target.Contains("/"))
            {
                target = target.Split('/').Last();
            }
            
            return $"Move folder '{target}' to '{destination}'";
        }

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("Target", "");
            AddDefaultConfigurationKey("Destination",  "");
        }

        public override Task<StageResult> DoTaskAsync()
        {
            TryGetConfigValue("Target", out string target);
            TryGetConfigValue("Destination", out string destination);
            target = target.Replace(@"\", "/").Replace("//", "/").TrimEnd('/');
            destination = destination.Replace(@"\", "/").Replace("//", "/").TrimEnd('/');

            if (!Directory.Exists(target))
            {
                FailureReason = $"Directory '{target}' does not exist.";
                return Task.FromResult(StageResult.Failed);
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
                        return Task.FromResult(StageResult.Failed);
                    }
                }
            }

            OnConsoleOut($"UBT: Moving folder '{target}' to '{destination}'");
            Directory.Move(target, destination);
            return Task.FromResult(StageResult.Successful);
        }
    }
}