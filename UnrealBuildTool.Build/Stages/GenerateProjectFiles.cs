using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace UnrealBuildTool.Build.Stages
{
    public class GenerateProjectFiles : BuildStage
    {
        public override string GetName() => "GenerateProjectFiles";

        public override string GetDescription() => "Generate Project Files";

        public override Task<StageResult> DoTaskAsync()
        {
            var UBTPath = $"{BuildConfig.EngineDirectory}/Engine/Binaries/DotNET/UnrealBuildTool.exe";
            UBTPath = UBTPath.Replace("//", "/");

            var arguments = new[]
            {
                "-projectfiles",
                $"-project=\"{BuildConfig.GetProjectFilePath()}\"",
                "-game",
                "-rocket",
                "-progress"
            };

            var process = new Process();
            var startInfo = new ProcessStartInfo
            {
                FileName = UBTPath,
                Arguments = string.Join(" ", arguments),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            process.StartInfo = startInfo;
            process.OutputDataReceived += (sender, args) =>
            {
                OnConsoleOut(args.Data);
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                Console.WriteLine(args.Data);
                OnConsoleError(args.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                return Task.FromResult(StageResult.Successful);
            }

            FailureReason = "An error has occured while generating project files.";
            return Task.FromResult(StageResult.Failed);
        }
    }
}