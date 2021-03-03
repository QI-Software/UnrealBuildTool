using System;

namespace UnrealBuildTool.Build
{
    public class StageConfigurationKey
    {
        public string Key { get; }
        
        public Type Type { get;  }
        
        public object DefaultValue { get; }

        public StageConfigurationKey(string key, Type type, object defaultValue)
        {
            Key = key;
            Type = type;
            DefaultValue = defaultValue;
        }
    }
}