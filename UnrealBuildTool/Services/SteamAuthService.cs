using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Serilog.Core;
using SteamAuth;
using UnrealBuildTool.Services.Models;

namespace UnrealBuildTool.Services
{
    public class SteamAuthService
    {
        private readonly Logger _log;
        private List<SteamworksUser> _steamAccounts;

        public SteamAuthService(Logger log)
        {
            _log = log;
        }

        public void Initialize()
        {
            if (!File.Exists("steam/accounts.json"))
            {
                _steamAccounts = new List<SteamworksUser>();
                SaveSteamAccounts();
            }
            else
            {
                try
                {
                    var json = File.ReadAllText("steam/accounts.json");
                    _steamAccounts = JsonConvert.DeserializeObject<List<SteamworksUser>>(json);
                }
                catch (Exception e)
                {
                    _log.Error(e, "[SteamAuthService] Failed to read Steamworks accounts, defaulting to none.");
                    _steamAccounts = new List<SteamworksUser>();
                }
            }
        }

        public bool AddSteamworksUser(string username, string password, Func<string> onTwoFactorRequired, out SteamworksUser newUser, out string errorReason)
        {
            errorReason = null;
            newUser = null;
            username = username?.Trim();
            password = password?.Trim();
            
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username cannot be null or whitespace.");
            }
            
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password cannot be null or whitespace.");
            }

            var existing = _steamAccounts.FirstOrDefault(u => u.Username.Equals(username, StringComparison.CurrentCultureIgnoreCase));
            if (existing != null)
            {
                newUser = existing;
                return true;
            }

            var login = new UserLogin(username, password);
            Retry:
            var result = login.DoLogin();

            switch (result)
            {
                case LoginResult.LoginOkay:
                    var auth = new AuthenticatorLinker(login.Session);
                    var authResult = auth.AddAuthenticator();
                    switch (authResult)
                    {
                        case AuthenticatorLinker.LinkResult.MustProvidePhoneNumber:
                            errorReason = "Steam account is missing a phone number";
                            return false;
                        case AuthenticatorLinker.LinkResult.MustRemovePhoneNumber:
                            errorReason = "Steam account mustn't have a phone number.";
                            return false;
                        case AuthenticatorLinker.LinkResult.GeneralFailure:
                            errorReason = "An unknown error has occured while adding an authenticator.";
                            return false;
                        case AuthenticatorLinker.LinkResult.AuthenticatorPresent:
                            errorReason = "This account already has an authenticator, please remove it.";
                            return false;
                    }
                    var user = new SteamworksUser
                    {
                        Username = username,
                        Password = password,
                        SteamGuard = auth.LinkedAccount
                    };
                    
                    _steamAccounts.Add(user);
                    var code = onTwoFactorRequired?.Invoke();
                    var finalizeResult = auth.FinalizeAddAuthenticator(code);
                    switch (finalizeResult)
                    {
                        case AuthenticatorLinker.FinalizeResult.BadSMSCode:
                            errorReason = "A bad SMS code was given for the authenticator.";
                            return false;
                        case AuthenticatorLinker.FinalizeResult.UnableToGenerateCorrectCodes:
                            errorReason = "Unable to generate correct codes.";
                            return false;
                        case AuthenticatorLinker.FinalizeResult.GeneralFailure:
                            errorReason = "An unknown error has occured while finalizing an authenticator.";
                            return false;
                    }
                    
                    SaveSteamAccounts();
                    newUser = user;
                    return true;
                case LoginResult.GeneralFailure:
                    errorReason = "Failed to login: unknown error.";
                    return false;
                case LoginResult.BadRSA:
                    errorReason = "Failed to login: bad RSA";
                    return false;
                case LoginResult.BadCredentials:
                    errorReason = "Wrong credentials entered, please try again.";
                    return false;
                case LoginResult.NeedCaptcha:
                    errorReason = "Steam requested a captcha to be completed, this cannot be handled.";
                    return false;
                case LoginResult.Need2FA:
                    login.TwoFactorCode = onTwoFactorRequired?.Invoke();
                    goto Retry;
                case LoginResult.NeedEmail:
                    login.EmailCode = onTwoFactorRequired?.Invoke();
                    goto Retry;
                case LoginResult.TooManyFailedLogins:
                    errorReason = "Too many login attempts have been made, please try again later.";
                    return false;
            }

            errorReason = "An unhandled error happened. Please try again";
            return false;
        }
        
        private void SaveSteamAccounts()
        {
            if (!Directory.Exists("steam"))
            {
                Directory.CreateDirectory("steam");
            }
            
            if (_steamAccounts != null)
            {
                var json = JsonConvert.SerializeObject(_steamAccounts, Formatting.Indented);
                File.WriteAllText("steam/accounts.json", json);
            }
        }
    }
}