using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            if (!File.Exists("config/steam/accounts.json"))
            {
                _steamAccounts = new List<SteamworksUser>();
                SaveSteamAccounts();
            }
            else
            {
                try
                {
                    var json = File.ReadAllText("config/steam/accounts.json");
                    _steamAccounts = JsonConvert.DeserializeObject<List<SteamworksUser>>(json);
                }
                catch (Exception e)
                {
                    _log.Error(e, "[SteamAuthService] Failed to read Steamworks accounts, defaulting to none.");
                    _steamAccounts = new List<SteamworksUser>();
                }
            }
        }

        public async Task<SteamworksAddUserResult> AddSteamworksUserAsync(string username, string password, Func<Task<string>> onTwoFactorRequired)
        {
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
                return SteamworksAddUserResult.FromSuccess(existing);
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
                            return SteamworksAddUserResult.FromFailure("Steam account is missing a phone number");
                        case AuthenticatorLinker.LinkResult.MustRemovePhoneNumber:
                            return SteamworksAddUserResult.FromFailure("Steam account mustn't have a phone number.");
                        case AuthenticatorLinker.LinkResult.GeneralFailure:
                            return SteamworksAddUserResult.FromFailure("An unknown error has occured while adding an authenticator.");
                        case AuthenticatorLinker.LinkResult.AuthenticatorPresent:
                            return SteamworksAddUserResult.FromFailure("This account already has an authenticator, please remove it.");
                    }
                    var user = new SteamworksUser
                    {
                        Username = username,
                        Password = password,
                        SteamGuard = auth.LinkedAccount
                    };

                    _steamAccounts.Add(user);
                    var code = await (onTwoFactorRequired?.Invoke() ?? Task.FromResult(""));
                    var finalizeResult = auth.FinalizeAddAuthenticator(code);
                    switch (finalizeResult)
                    {
                        case AuthenticatorLinker.FinalizeResult.BadSMSCode:
                            return SteamworksAddUserResult.FromFailure("A bad SMS code was given for the authenticator.");
                        case AuthenticatorLinker.FinalizeResult.UnableToGenerateCorrectCodes:
                            return SteamworksAddUserResult.FromFailure("Unable to generate correct codes.");
                        case AuthenticatorLinker.FinalizeResult.GeneralFailure:
                            return SteamworksAddUserResult.FromFailure("An unknown error has occured while finalizing an authenticator.");
                    }

                    SaveSteamAccounts();
                    return SteamworksAddUserResult.FromSuccess(user);
                case LoginResult.GeneralFailure:
                    return SteamworksAddUserResult.FromFailure("Failed to login: unknown error.");
                case LoginResult.BadRSA:
                    return SteamworksAddUserResult.FromFailure("Failed to login: bad RSA");
                case LoginResult.BadCredentials:
                    return SteamworksAddUserResult.FromFailure("Wrong credentials entered, please try again.");
                case LoginResult.NeedCaptcha:
                    return SteamworksAddUserResult.FromFailure("Steam requested a captcha to be completed, this cannot be handled.");
                case LoginResult.Need2FA:
                    login.TwoFactorCode = await (onTwoFactorRequired?.Invoke() ?? Task.FromResult(""));
                    goto Retry;
                case LoginResult.NeedEmail:
                    login.EmailCode = await (onTwoFactorRequired?.Invoke() ?? Task.FromResult(""));
                    goto Retry;
                case LoginResult.TooManyFailedLogins:
                    return SteamworksAddUserResult.FromFailure("Too many login attempts have been made, please try again later.");
            }

            return SteamworksAddUserResult.FromFailure("An unhandled error happened. Please try again");
        }

        public bool HasAccount(string name) => _steamAccounts.Any(u => u.Username.Equals(name ?? "", StringComparison.CurrentCultureIgnoreCase));

        private void SaveSteamAccounts()
        {
            if (!Directory.Exists("steam"))
            {
                Directory.CreateDirectory("steam");
            }

            if (_steamAccounts != null)
            {
                var json = JsonConvert.SerializeObject(_steamAccounts, Formatting.Indented);
                File.WriteAllText("config/steam/accounts.json", json);
            }
        }
    }
}