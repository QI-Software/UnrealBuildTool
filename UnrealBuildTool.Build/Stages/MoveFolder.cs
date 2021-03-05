using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class MoveFolder : BuildStage
    {
        public override string GetName() => "MoveFolder";

        public override string GetDescription()
        {
            TryGetConfigValue<string>("Target", out var target);
            TryGetConfigValue<string>("Destination", out var destination);

            target = target.Replace(@"\", "/").Replace("//", "/").TrimEnd('/');
            destination = destination.Replace(@"\", "/").Replace("//", "/").TrimEnd('/');

            if (target.Contains("/"))
            {
                target = target.Split('/').Last();
            }
            
            if (destination.Contains("/"))
            {
                destination = destination.Split('/').Last();
            }

            return $"Move folder '{target}' to '{destination}'";
        }

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("Target", "");
            AddDefaultConfigurationKey("Destination",  "");
        }

        public override Task<StageResult> DoTaskAsync()
        {
            TryGetConfigValue<string>("Target", out var target);
            TryGetConfigValue<string>("Destination", out var destination);

            if (!Directory.Exists(target))
            {
                FailureReason = $"Directory '{target}' does not exist.";
                return Task.FromResult(StageResult.Failed);
            }

            OnConsoleOut($"UBT: Moving folder '{target}' to '{destination}'");
            Directory.Move(target, destination);
            return Task.FromResult(StageResult.Successful);
        }
    }
}