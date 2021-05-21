namespace UnrealBuildTool.Services.Models
{
    public struct SteamworksAddUserResult
    {
        public bool Success { get; internal set; }
        
        public string ErrorMessage { get; internal set; }
        
        public SteamworksUser NewUser { get; internal set; }

        internal SteamworksAddUserResult(bool success, string errorMessage, SteamworksUser newUser)
        {
            Success = success;
            ErrorMessage = errorMessage;
            NewUser = newUser;
        }

        internal static SteamworksAddUserResult FromSuccess(SteamworksUser user)
        {
            return new SteamworksAddUserResult(true, null, user);
        }

        internal static SteamworksAddUserResult FromFailure(string errorMessage)
        {
            return new SteamworksAddUserResult(false, errorMessage, null);
        }
    }
}