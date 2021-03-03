using System.Collections.Generic;

namespace UnrealBuildTool.Build
{
    public class BuildConfigurationTemplate : BuildConfiguration
    {
        public BuildConfigurationTemplate()
        {
            Name = "Build Configuration Name";
            Description = "Build Configuration Description";
            ProjectDirectory = "/Path/To/Project.uproject";
            EngineDirectory = "/Path/To/Engine";
            Stages = new Dictionary<string, Dictionary<string, object>>
            {
                {
                    "BuildStageName", new Dictionary<string, object>
                    {
                        {"ConfigKey", "Value"},
                        {"AnotherKey", true}
                    }
                }
            };
        }
    }
}