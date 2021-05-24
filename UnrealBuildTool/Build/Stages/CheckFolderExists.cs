using System;
using System.IO;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class CheckFolderExists : BuildStage
    {
        public override string GetName() => nameof(CheckFolderExists);

        public override string GetDescription()
        {
            TryGetConfigValue("FolderPath", out string path);
            path = path.Replace(@"\", "/").Replace("//", "/").TrimEnd('/');
            return $"Check folder '{path}'";
        }

        public override bool IsStageConfigurationValid(out string ErrorMessage)
        {
            if (!base.IsStageConfigurationValid(out ErrorMessage))
            {
                return false;
            }

            TryGetConfigValue("FolderPath", out string path);
            if (string.IsNullOrWhiteSpace(path))
            {
                ErrorMessage = $"Cannot check null or empty folder path.";
                return false;
            }

            return true;
        }

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("FolderPath", "");
        }

        public override Task<StageResult> DoTaskAsync(IServiceProvider services)
        {
            TryGetConfigValue("FolderPath", out string path);
            path = path.Replace(@"\", "/").Replace("//", "/").TrimEnd('/');

            if (Directory.Exists(path))
            {
                return Task.FromResult(StageResult.Successful);
            }

            FailureReason = $"Could not find folder at '{path}'";
            return Task.FromResult(StageResult.Failed);
        }
    }
}