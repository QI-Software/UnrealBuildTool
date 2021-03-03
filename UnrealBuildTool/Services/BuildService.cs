using Serilog;
using Serilog.Core;

namespace UnrealBuildTool.Services
{
    public class BuildService
    {
        private readonly Logger _log;

        public BuildService(Logger log)
        {
            _log = log;
        }

        public bool IsBuilding()
        {
            return false;
        }
    }
}