using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class CleanupProjectFiles : BuildStage
    {
        public override string GetName()
        {
            return "CleanupProjectFiles";
        }

        public override string GetDescription()
        {
            return "Clean up Binaries and Intermediate folders";
        }

        public override Task<StageResult> DoTaskAsync()
        {
            var binariesPath = $"{BuildConfig.ProjectDirectory}/Binaries";
            var intermediatePath = $"{BuildConfig.ProjectDirectory}/Intermediate";
            binariesPath = binariesPath.Replace("//", "/");
            intermediatePath = intermediatePath.Replace("//", "/");
            
            if (Directory.Exists(binariesPath))
            {
                Directory.Delete(binariesPath, true);
                OnConsoleOut("Deleted Binaries folder.");
            }
            else
            {
                OnConsoleOut("No Binaries folder to delete.");
            }

            if (Directory.Exists(intermediatePath))
            {
                OnConsoleOut("Deleted Intermediate folder.");
                Directory.Delete(intermediatePath, true);
            }
            else
            {
                OnConsoleOut("No Intermediate folder to delete.");
            }

            return Task.FromResult(StageResult.Successful);
        }

        // Don't need to do anything.
        public override Task OnCancellationRequestedAsync()
        {
            base.OnCancellationRequestedAsync();
            return Task.CompletedTask;
        }
    }
}