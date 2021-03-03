using System;
using System.Threading.Tasks;

namespace UnrealBuildTool
{
    class Program
    {
        private static UnrealBuildTool _ubt;
        
        static async Task Main(string[] args)
        {
            _ubt = new UnrealBuildTool();
            await _ubt.StartAsync(args);
        }
    }
}