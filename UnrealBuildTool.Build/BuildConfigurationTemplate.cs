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
            Stages = new List<BuildConfigurationStage>()
            {
                new BuildConfigurationStage
                {
                    Name = "StageName",
                    Configuration = new Dictionary<string, object>
                    {
                        { "Key1", "Value1" },
                        { "BoolValue", true },
                        { "SomeNumber", 1337 }
                    }
                }
            };
        }
    }
}