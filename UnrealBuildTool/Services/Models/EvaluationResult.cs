using System;

namespace UnrealBuildTool.Models
{
    public class EvaluationResult
    {
        public readonly Exception Exception;

        public readonly object Result;
        
        public EvaluationResult(object result, Exception exception)
        {
            Exception = exception;
            Result = result;
        }
    }
}