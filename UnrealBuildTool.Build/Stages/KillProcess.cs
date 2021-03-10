using System.Diagnostics;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class KillProcess : BuildStage
    {
        public override string GetName() => "KillProcess";

        public override string GetDescription()
        {
            TryGetConfigValue("ProcessName", out string name);
            return $"Kill process '{name}'";
        }

        public override bool IsStageConfigurationValid(out string ErrorMessage)
        {
            if (!base.IsStageConfigurationValid(out ErrorMessage))
            {
                return false;
            }

            TryGetConfigValue("ProcessName", out string name);
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Cannot kill process with null or empty name.";
                return false;
            }

            return true;
        }

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("ProcessName", "");
            AddDefaultConfigurationKey("KillChildren", true);
        }

        public override Task<StageResult> DoTaskAsync()
        {
            TryGetConfigValue("ProcessName", out string name);
            TryGetConfigValue("KillChildren", out bool killChildren);

            OnConsoleOut($"UBT: Searching for process '{name}'");

            foreach (var process in Process.GetProcessesByName(name))
            {
                if (process != null && !process.HasExited)
                {
                    OnConsoleOut($"UBT: Killing process [{process.Id}] {process.ProcessName}");
                    process.Kill(killChildren);
                }
            }

            return Task.FromResult(StageResult.Successful);
        }
    }
}