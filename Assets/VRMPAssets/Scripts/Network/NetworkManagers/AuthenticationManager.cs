using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

#if HAS_MPPM
using UnityEngine.XR.Interaction.Toolkit.UI;
#endif

#if UNITY_EDITOR
#if HAS_PARRELSYNC
using ParrelSync;
#endif
#endif

namespace XRMultiplayer
{
    public class AuthenticationManager : MonoBehaviour
    {
        const string k_DebugPrepend = "<color=#938FFF>[Authentication Manager]</color> ";
        const int k_MaxProfileLength = 30;
        const string k_InvalidProfileCharacters = @"[^\w-]";
        const string k_ProfileArg = "-ugsProfile";
        const string k_LegacyPlayerArgId = "PlayerArg";
        const string k_DefaultEditorProfile = "EditorMain";
        const string k_DefaultBuildProfilePrefix = "Build";
        static readonly string s_RuntimeInstanceTag = Guid.NewGuid().ToString("N").Substring(0, 8);

        [Header("Authentication Profile")]
        [SerializeField] bool m_UseCommandLineArgs = true;
        [SerializeField] string m_ProfileOverride = "";
        [SerializeField] bool m_ClearCredentialsWhenSwitchingProfile = false;
        [SerializeField] bool m_LogResolvedProfile = true;

#if HAS_MPPM
        const string k_MppmEditorName = "-name";
        const string k_MppmCloneProcess = "--virtual-project-clone";
        const string k_MppmVirtualProjectIdArg = "-vpId";

        bool m_IsVirtualPlayer;

        public XRUIInputModule inputModule
        {
            get
            {
                if (m_InputModule == null)
                {
                    m_InputModule = FindAnyObjectByType<XRUIInputModule>();
                }

                return m_InputModule;
            }
        }

        XRUIInputModule m_InputModule;
#endif

        /// <summary>
        /// Initializes UGS and signs in anonymously with an isolated local profile.
        /// </summary>
        /// <remarks>
        /// Each local process must use a different profile to avoid cached-session collisions
        /// when running host + client on the same machine.
        /// </remarks>
        public virtual async Task<bool> Authenticate()
        {
            try
            {
                var targetProfile = ResolveTargetProfile();
                if (string.IsNullOrEmpty(targetProfile))
                {
                    Utils.LogError($"{k_DebugPrepend}Unable to resolve a valid Authentication profile.");
                    return false;
                }

                if (m_LogResolvedProfile)
                {
                    Utils.Log($"{k_DebugPrepend}Target Authentication profile: {targetProfile}");
                }

                await EnsureServicesInitializedAsync(targetProfile);
                await EnsureProfileAndSignInAsync(targetProfile);

                XRINetworkGameManager.AuthenicationId = AuthenticationService.Instance.PlayerId;
                Utils.Log($"{k_DebugPrepend}Authenticated PlayerId={XRINetworkGameManager.AuthenicationId} Profile={AuthenticationService.Instance.Profile}");

                // Lobby/Relay join must start after this method returns true.
                // Example: XRINetworkGameManager.Awake -> await Authenticate() -> QuickJoin/Create lobby.
                return UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;
            }
            catch (AuthenticationException e)
            {
                Utils.LogError($"{k_DebugPrepend}AuthenticationException: {e.ErrorCode} - {e.Message}");
                return false;
            }
            catch (RequestFailedException e)
            {
                Utils.LogError($"{k_DebugPrepend}RequestFailedException: {e.ErrorCode} - {e.Message}");
                return false;
            }
            catch (Exception e)
            {
                Utils.LogError($"{k_DebugPrepend}Error during authentication: {e}");
                return false;
            }
        }

        public static bool IsAuthenticated()
        {
            try
            {
                return AuthenticationService.Instance.IsSignedIn;
            }
            catch (Exception e)
            {
                Utils.Log($"{k_DebugPrepend}Checking Authentication before initialization: {e}");
                return false;
            }
        }

        async Task EnsureServicesInitializedAsync(string profile)
        {
            while (UnityServices.State == ServicesInitializationState.Initializing)
            {
                await Task.Yield();
            }

            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var options = new InitializationOptions().SetProfile(profile);
                await UnityServices.InitializeAsync(options);
            }
            else if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                throw new InvalidOperationException($"Unexpected UnityServices state: {UnityServices.State}");
            }
        }

        async Task EnsureProfileAndSignInAsync(string targetProfile)
        {
            var auth = AuthenticationService.Instance;
            if (auth == null)
            {
                throw new InvalidOperationException("AuthenticationService.Instance is null after UnityServices initialization.");
            }

            if (!string.Equals(auth.Profile, targetProfile, StringComparison.Ordinal))
            {
                SwitchProfileSafely(targetProfile);
            }

            if (!auth.IsSignedIn)
            {
                await auth.SignInAnonymouslyAsync();
            }
        }

        void SwitchProfileSafely(string profile)
        {
            var auth = AuthenticationService.Instance;
            if (auth == null)
            {
                throw new InvalidOperationException("AuthenticationService.Instance is null.");
            }

            // SwitchProfile only succeeds when signed out.
            if (auth.IsSignedIn)
            {
                auth.SignOut(m_ClearCredentialsWhenSwitchingProfile);
            }

            try
            {
                auth.SwitchProfile(profile);
            }
            catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.ClientInvalidUserState)
            {
                // Retry once after forcing signed-out state, in case another system changed auth state.
                auth.SignOut(m_ClearCredentialsWhenSwitchingProfile);
                auth.SwitchProfile(profile);
            }
        }

        string ResolveTargetProfile()
        {
            if (!string.IsNullOrWhiteSpace(m_ProfileOverride))
            {
                return SanitizeProfile(m_ProfileOverride);
            }

            if (m_UseCommandLineArgs)
            {
                var cliProfile = GetProfileFromCommandLine();
                if (!string.IsNullOrEmpty(cliProfile))
                {
                    return SanitizeProfile(cliProfile);
                }
            }

#if UNITY_EDITOR
#if HAS_MPPM
            var mppmProfile = CheckMPPM();
            if (!string.IsNullOrEmpty(mppmProfile))
            {
                return SanitizeProfile($"MPPM_{mppmProfile}");
            }
#elif HAS_PARRELSYNC
            var parrelProfile = CheckParrelSync();
            if (!string.IsNullOrEmpty(parrelProfile))
            {
                return SanitizeProfile($"Parrel_{parrelProfile}");
            }
#endif
            return SanitizeProfile(k_DefaultEditorProfile);
#else
            // Build fallback: a per-process runtime tag keeps simultaneous local instances on separate profiles.
            return SanitizeProfile($"{k_DefaultBuildProfilePrefix}_{s_RuntimeInstanceTag}");
#endif
        }

        static string GetProfileFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg.Equals(k_ProfileArg, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    return args[i + 1];
                }

                if (arg.StartsWith($"{k_ProfileArg}=", StringComparison.OrdinalIgnoreCase))
                {
                    return arg.Substring(k_ProfileArg.Length + 1);
                }

                if (arg.IndexOf(k_LegacyPlayerArgId, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var splitArgs = arg.Split(':');
                    if (splitArgs.Length > 1)
                    {
                        return splitArgs[1];
                    }
                }
            }

            return string.Empty;
        }

        static string SanitizeProfile(string rawProfile)
        {
            if (string.IsNullOrWhiteSpace(rawProfile))
            {
                return "Player";
            }

            var sanitized = Regex.Replace(rawProfile.Trim(), k_InvalidProfileCharacters, "");
            if (sanitized.Length > k_MaxProfileLength)
            {
                sanitized = sanitized.Substring(0, k_MaxProfileLength);
            }

            return string.IsNullOrEmpty(sanitized) ? "Player" : sanitized;
        }

#if UNITY_EDITOR
#if HAS_MPPM
        string CheckMPPM()
        {
            Utils.Log($"{k_DebugPrepend}MPPM Found");
            var mppmName = "";
            var mppmVirtualProjectId = "";

            var arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; ++i)
            {
                if (arguments[i] == k_MppmCloneProcess)
                {
                    m_IsVirtualPlayer = true;
                    var module = inputModule;
                    if (module != null)
                    {
                        module.enableMouseInput = false;
                        module.enableTouchInput = false;
                    }
                }

                if (arguments[i] == k_MppmEditorName && (i + 1) < arguments.Length)
                {
                    mppmName = arguments[i + 1];
                }

                if (arguments[i].StartsWith($"{k_MppmVirtualProjectIdArg}=", StringComparison.OrdinalIgnoreCase))
                {
                    mppmVirtualProjectId = ReadNameValueArgument(arguments[i]);
                }
            }

            if (!string.IsNullOrEmpty(mppmVirtualProjectId))
            {
                return mppmVirtualProjectId;
            }

            if (!string.IsNullOrEmpty(mppmName))
            {
                return mppmName;
            }

            if (m_IsVirtualPlayer)
            {
                Utils.LogWarning("MPPM virtual player detected without clone name. Falling back to runtime-instance profile.");
                return s_RuntimeInstanceTag;
            }

            return string.Empty;
        }

        static string ReadNameValueArgument(string arg)
        {
            var separatorIndex = arg.IndexOf('=');
            if (separatorIndex < 0 || separatorIndex + 1 >= arg.Length)
            {
                return string.Empty;
            }

            return arg.Substring(separatorIndex + 1);
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus && m_IsVirtualPlayer)
            {
                var module = inputModule;
                if (module != null)
                {
                    module.enableMouseInput = true;
                    module.enableTouchInput = true;
                }
            }
        }
#endif

#if HAS_PARRELSYNC
        string CheckParrelSync()
        {
            Utils.Log($"{k_DebugPrepend}ParrelSync Found");
            return ClonesManager.IsClone() ? ClonesManager.GetArgument() : string.Empty;
        }
#endif
#endif
    }
}

