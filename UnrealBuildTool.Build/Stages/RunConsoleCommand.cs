using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class RunConsoleCommand : BuildStage
    {
        public override string GetName() => "RunConsoleCommand";

        public override string GetDescription() => $"Running command";

        public override void GenerateDefaultStageConfiguration()
        {
            base.GenerateDefaultStageConfiguration();
            
            AddDefaultConfigurationKey("Command", typeof(string), "");
        }

        public override Task<StageResult> DoTaskAsync()
        {
            throw new System.NotImplementedException();
        }

        // TODO: Done
    }
}