using System;
using Newtonsoft.Json;

namespace UnrealBuildTool.Services.Models
{
    public class BuildSchedule
    {
        /// <summary>
        /// The name of this schedule.
        /// </summary>
        [JsonProperty] 
        public string Name { get; internal set; }
        
        /// <summary>
        /// The name of the build configuration to run. Relative to the build configs folder.
        /// </summary>
        [JsonProperty] 
        public string ConfigurationName { get; internal set; }

        /// <summary>
        /// Date at which this build schedule will start running.
        /// </summary>
        [JsonProperty]
        public DateTimeOffset StartDate  { get; internal set; }

        /// <summary>
        /// Next date at which this build schedule should be executed, if any.
        /// </summary>
        [JsonProperty]
        public DateTimeOffset? NextRunDate  { get; internal set; }

        /// <summary>
        /// Amount of time to wait between each runs of this schedule.
        /// </summary>
        [JsonProperty] 
        public TimeSpan RepeatInterval  { get; internal set; }
        
        [JsonIgnore]
        public string FilePath { get; internal set; }
    }
}