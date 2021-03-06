using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace UnrealBuildTool.Build
{
    public class BuildConfiguration
    {
        /// <summary>
        /// The name of the build configuration as it will be referred to.
        /// </summary>
        [JsonProperty]
        public string Name { get; protected set; }

        /// <summary>
        /// The description of the build configuration.
        /// </summary>
        [JsonProperty]
        public string Description { get; protected set; }
        
        /// <summary>
        /// Absolute path to the project's directory.
        /// </summary>
        [JsonProperty]
        public string ProjectDirectory { get; protected set; }
        
        /// <summary>
        /// Name of the .uproject file in the project's directory.
        /// </summary>
        [JsonProperty]
        public string ProjectFile { get; protected set; }
        
        /// <summary>
        /// Name of the .sln file in the project's directory.
        /// </summary>
        [JsonProperty]
        public string SolutionFile { get; protected set; }

        /// <summary>
        /// Absolute path to the engine's directory.
        /// </summary>
        [JsonProperty]
        public string EngineDirectory { get; protected set; }
        
        /// <summary>
        /// The build's stages in order, name as key, configuration map as value.
        /// </summary>
        [JsonProperty]
        public List<BuildConfigurationStage> Stages { get; protected set; }
        
        /// <summary>
        /// The source file this build configuration was loaded from.
        /// </summary>
        public string SourceFile { get; set; }

        public string GetProjectFilePath()
        {
            return $"{ProjectDirectory}/{ProjectFile}".Replace("//", "/");
        }
        
        public string GetSolutionFilePath()
        {
            return $"{ProjectDirectory}/{SolutionFile}".Replace("//", "/");
        }

        public string GetUnrealBuildToolPath()
        {
            return $"{EngineDirectory}/Engine/Binaries/DotNET/UnrealBuildTool.exe".Replace("//", "/");
        }

        private static List<string> _configurations = new List<string>()
        {
            "Shipping",
            "Debug",
            "DebugGame",
            "Development",
            "Test",
        };
        
        public static bool IsValidConfiguration(string configuration)
        {
            return _configurations.Any(a => a == configuration);
        }
    }
}