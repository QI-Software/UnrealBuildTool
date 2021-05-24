using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace UnrealBuildTool.Build
{
    public class BuildConfigurationStage
    {
        [JsonProperty] 
        public string Name;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Dictionary<string, object> Configuration = new Dictionary<string, object>();
    }
}