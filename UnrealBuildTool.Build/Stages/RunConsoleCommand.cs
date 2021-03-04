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
            AddDefaultConfigurationKey("RunInProjectDirectory", typeof(bool), true);
            AddDefaultConfigurationKey("RunInEngineDirectory", typeof(bool), false);
        }
        
        // TODO: Done
    }
}