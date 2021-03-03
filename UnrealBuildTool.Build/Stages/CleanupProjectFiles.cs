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
            return "Cleaning up Binaries and Intermediate folders";
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
            }

            if (Directory.Exists(intermediatePath))
            {
                Directory.Delete(intermediatePath, true);
            }

            return Task.FromResult<StageResult>(StageResult.Successful);
        }
    }
}