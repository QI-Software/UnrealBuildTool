﻿using System.Collections.Generic;
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
        /// Absolute path to the engine's directory.
        /// </summary>
        [JsonProperty]
        public string EngineDirectory { get; protected set; }
        
        /// <summary>
        /// The build's stages in order, name as key, configuration map as value.
        /// </summary>
        [JsonProperty]
        public Dictionary<string, Dictionary<string, object>> Stages { get; protected set; }
    }
}