using System.Collections.Generic;
using Newtonsoft.Json;

namespace UnrealBuildTool.Build
{
    public class BuildStageTemplate
    {
        [JsonProperty]
        public string StageName { get; private set; }
        
        [JsonProperty]
        public string StageDescription { get; private set; }
        
        [JsonProperty]
        public Dictionary<string, object> StageConfiguration { get; private set; }

        public BuildStageTemplate(BuildStage inStage)
        {
            StageName = inStage.GetName();
            StageDescription = inStage.GetDescription();
            StageConfiguration = inStage.GetStageConfiguration();
        }
    }
}