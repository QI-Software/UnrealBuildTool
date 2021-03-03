using System;
using System.Collections.Generic;

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

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("DeleteSavedFolder", typeof(bool), true);
            AddDefaultConfigurationKey("CumCumCum", typeof(string), "based");
            AddDefaultConfigurationKey("SomeNumber", typeof(int), 1337);
        }
    }
}