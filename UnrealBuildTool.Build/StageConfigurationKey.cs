using System;

namespace UnrealBuildTool.Build
{
    public class StageConfigurationKey
    {
        public string Key { get; private set; }
        
        public Type Type { get; private set; }
        
        public object DefaultValue { get; private set; }

        public StageConfigurationKey(string key, Type type, object defaultValue)
        {
            Key = key;
            Type = type;
            DefaultValue = defaultValue;
        }
    }
}