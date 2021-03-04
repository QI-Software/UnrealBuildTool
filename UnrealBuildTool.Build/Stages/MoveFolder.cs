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

            target = target.Replace(@"\", "/").Replace("//", "/");
            destination = destination.Replace(@"\", "/").Replace("//", "/");

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
            
            AddDefaultConfigurationKey("Target", typeof(string), "");
            AddDefaultConfigurationKey("Destination", typeof(string), "");
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

            if (!Directory.Exists(destination))
            {
                FailureReason = $"Directory '{destination}' does not exist.";
                return Task.FromResult(StageResult.Failed);
            }

            OnConsoleOut($"UBT: Moving folder '{target}' to '{destination}'");
            Directory.Move(target, destination);
            return Task.FromResult(StageResult.Successful);
        }
    }
}