using System;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build
{
    public class AutomatedBuild
    {
        private readonly BuildConfiguration _buildConfig;
        
        public AutomatedBuild(BuildConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new NullReferenceException("Cannot instantiate a Build with a null BuildConfiguration.");
            }
            
            _buildConfig = configuration;
        }

        public async Task StartBuildAsync()
        {
            
        }
    }
}