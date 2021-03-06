using System;
using System.IO;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class DeleteFolder : BuildStage
    {
        public override string GetName() => nameof(DeleteFolder);

        public override string GetDescription()
        {
            TryGetConfigValue("FolderPath", out string path);
            return $"Delete folder '{path}'";
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
                ErrorMessage = $"Invalid path specified: {path}";
                return false;
            }

            return true;
        }

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("FolderPath", "");
            AddDefaultConfigurationKey("IsCritical", false);
        }

        public override Task<StageResult> DoTaskAsync()
        {
            TryGetConfigValue("FolderPath", out string path);
            TryGetConfigValue("IsCritical", out bool critical);

            path = path.Replace(@"\", "/").Replace("//", "/").TrimEnd('/');
            
            if (Directory.Exists(path))
            {
                OnConsoleOut($"UBT: Deleting folder '{path}'");
                try
                {
                    Directory.Delete(path, true);
                }
                catch (Exception e)
                {
                    FailureReason = $"UBT: Failed to delete directory '{path}': {e.Message}";
                    OnConsoleError(FailureReason);
                    return Task.FromResult(critical ? StageResult.Failed : StageResult.SuccessfulWithWarnings);
                }
            }
            else
            {
                OnConsoleOut($"UBT: No directory to delete at '{path}', ignoring.");
            }

            return Task.FromResult(StageResult.Successful);
        }
    }
}