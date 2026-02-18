using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Services.Vivox;
using Unity.XR.CoreUtils.Bindings.Variables;
using UnityEngine;
using UnityEngine.Android;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XRMultiplayer
{
    /// <summary>
    /// Manages the Vivox Voice Chat functionality in the VR Multiplayer template.
    /// </summary>
    public class VoiceChatManager : MonoBehaviour
    {
        /// <summary>
        /// String used to notify the player that they need to enable microphone permissions.
        /// </summary>
        const string k_MicrophonePersmissionDialogue = "Microphone Permissions Required.";

        /// <summary>
        public static BindableVariable<bool> s_HasMicrophonePermission = new(false);
        /// <summary>
        /// Dictionary of all the <see cref="XRINetworkPlayer"/>'s in the voice chat.
        /// </summary>
        public static Dictionary<string, XRINetworkPlayer> m_PlayersDictionary = new();

        /// <summary>
        /// This is the bindable variable for subscribing to the local player muting themselves.
        /// </summary>
        public IReadOnlyBindableVariable<bool> selfMuted
        {
            get => m_SelfMuted;
        }
        readonly BindableVariable<bool> m_SelfMuted = new(false);

        /// <summary>
        /// This is the bindable variable for subscribing to the connection status of the voice chat service.
        /// </summary>
        public IReadOnlyBindableVariable<string> connectionStatus
        {
            get => m_ConnectionStatus;
        }
        readonly BindableVariable<string> m_ConnectionStatus = new();

        /// <summary>
        /// The chat capability of the channel, by default it should Audio Only.
        /// </summary>
        [SerializeField, Tooltip("The chat capability of the channel, by default it should Audio Only")] ChatCapability m_ChatCapability = ChatCapability.AudioOnly;

        /// <summary>
        /// Update frequency for audio callbacks.
        /// </summary>
        [SerializeField, Tooltip("Update frequency for audio callbacks")] ParticipantPropertyUpdateFrequency m_UpdateFrequency = ParticipantPropertyUpdateFrequency.TenPerSecond;

        /// <summary>
        /// The maximum distance from the listener that a speaker can be heard.
        /// </summary>
        public int AudibleDistance
        {
            get => m_AudibleDistance;
            set => m_AudibleDistance = value;
        }
        [Header("Voice Chat Properties")]
        [SerializeField, Tooltip("The maximum distance from the listener that a speaker can be heard.")]
        int m_AudibleDistance = 32;

        /// <summary>
        /// The distance from the listener within which a speaker’s voice is heard at its original volume, and beyond which the speaker's voice begins to fade.
        /// </summary>
        public int ConversationalDistance
        {
            get => m_ConversationalDistance;
            set => m_ConversationalDistance = value;
        }
        [SerializeField, Tooltip("The distance from the listener within which a speaker’s voice is heard at its original volume, and beyond which the speaker's voice begins to fade.")]
        int m_ConversationalDistance = 7;

        /// <summary>
        /// The strength of the audio fade effect as the speaker moves away from the listener past the conversational distance.
        /// </summary>
        public float AudioFadeIntensity
        {
            get => m_AudioFadeIntensity;
            set => m_AudioFadeIntensity = value;
        }
        [SerializeField, Tooltip("The strength of the audio fade effect as the speaker moves away from the listener past the conversational distance.")]
        float m_AudioFadeIntensity = 1.0f;

        /// <summary>
        /// The model that determines the distance falloff of the voice chat.
        /// </summary>
        /// The strength of the audio fade effect as the speaker moves away from the listener past the conversational distance.
        /// </summary>
        public AudioFadeModel AudioFadeModel
        {
            get => m_AudioFadeModel;
            set => m_AudioFadeModel = value;
        }
        [SerializeField, Tooltip("The model that determines the distance falloff of the voice chat.")]
        AudioFadeModel m_AudioFadeModel = AudioFadeModel.LinearByDistance;

        /// <summary>
        /// The minimum and maximum volume for the voice output.
        /// </summary>
        [SerializeField, Tooltip("The minimum and maximum volume for the voice output.")] Vector2 m_MinMaxVoiceOutputVolume = new Vector2(-10.0f, 10.0f);

        /// <summary>
        /// The minimum and maximum volume for the voice input.
        /// </summary>
        [SerializeField, Tooltip("The minimum and maximum volume for the voice input.")] Vector2 m_MinMaxVoiceInputVolume = new Vector2(-10.0f, 10.0f);

        /// <summary>
        /// The local participant in the voice chat.
        /// </summary>
        VivoxParticipant m_LocalParticpant;

        /// <summary>
        /// The current lobby id the player is connected to.
        /// </summary>
        string m_CurrentLobbyId;

        /// <summary>
        /// If the player is connected to a room.
        /// </summary>
        bool m_ConnectedToRoom;

        /// <summary>
        /// If the voice chat service is initialized.
        /// </summary>
        bool m_IsInitialized;
        bool m_HasInitializationAttempted;
        bool m_IsVivoxAvailable = true;
        const string k_DebugPrepend = "<color=#0CFAFA>[Voice Chat Manager]</color> ";
        ///<inheritdoc/>
        private void Awake()
        {
            m_ConnectedToRoom = false;
        }

        void Start()
        {
            // Voice chat is not available in local only sessions.
            if (XRINetworkGameManager.CurrentSessionType == SessionType.LocalOnly)
                return;

            XRINetworkGameManager.CurrentConnectionState.Subscribe(ConnectionStateUpdated);
            XRINetworkGameManager.Connected.Subscribe(ConnectedToGame);
        }

        ///<inheritdoc/>
        private void OnDestroy()
        {
            if (VivoxService.Instance != null)
            {
                VivoxService.Instance.LoggedIn -= LocalUserLoggedIn;
                UnbindParticipantEvents();
            }

            // Voice chat is not available in local only sessions.
            if (XRINetworkGameManager.CurrentSessionType == SessionType.LocalOnly)
                return;

            XRINetworkGameManager.CurrentConnectionState.Unsubscribe(ConnectionStateUpdated);
            XRINetworkGameManager.Connected.Unsubscribe(ConnectedToGame);
        }

        /// <summary>
        /// Callback for when the local player connection state is updated.
        /// </summary>
        /// <param name="connected">Wether or not a player is connected.</param>
        void ConnectedToGame(bool connected)
        {
            if (!m_IsInitialized || !m_IsVivoxAvailable) return;

            if (connected)
            {
                Login();
            }
            else
            {
                LogOut();
            }
        }

        void ConnectionStateUpdated(XRINetworkGameManager.ConnectionState connectionState)
        {
            if (!m_IsVivoxAvailable || m_HasInitializationAttempted)
            {
                return;
            }

            if (!m_IsInitialized && connectionState == XRINetworkGameManager.ConnectionState.Authenticated)
            {
                Utils.Log($"{k_DebugPrepend}Initializing Voice Chat");
                m_ConnectionStatus.Value = "Initializing Voice Service";
                m_HasInitializationAttempted = true;
                EnableVoiceChat();
                if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                {
                    StartCoroutine(ShowPermissionsAfterDelay());
                }
                else
                {
                    MicrophonePermissionGranted();
                }
            }
        }

        IEnumerator ShowPermissionsAfterDelay(float delay = 1.0f)
        {
            Utils.Log($"{k_DebugPrepend}Requesting Microphone Permissions");
            PlayerHudNotification.Instance.ShowText("Requesting Microphone Permissions", 3.0f);
            yield return new WaitForSeconds(delay);
            PermissionCallbacks permissionCallbacks = new();
            permissionCallbacks.PermissionDenied += PermissionDeniedCallback;
            permissionCallbacks.PermissionGranted += PermissionGrantedCallback;
            Permission.RequestUserPermission(Permission.Microphone, permissionCallbacks);
        }

        void PermissionGrantedCallback(string permissionName)
        {
            if (permissionName == Permission.Microphone)
            {
                MicrophonePermissionGranted();
            }
        }

        void PermissionDeniedCallback(string permissionName)
        {
            if (permissionName == Permission.Microphone)
            {
                PlayerHudNotification.Instance.ShowText("Microphone Permissions Denied", 3.0f);
            }
        }

        void MicrophonePermissionGranted()
        {
            Utils.Log($"{k_DebugPrepend}Microphone Permissions Granted");
            s_HasMicrophonePermission.Value = true;
            PlayerHudNotification.Instance.ShowText("Microphone Permissions Granted", 3.0f);
        }

        async void EnableVoiceChat()
        {
            try
            {
                var vivox = VivoxService.Instance;
                if (vivox == null)
                {
                    throw new InvalidOperationException("Vivox service instance is null. Verify Vivox package configuration.");
                }

                await vivox.InitializeAsync();
                m_IsInitialized = true;
                m_ConnectionStatus.Value = "Voice Service Initialized";
                vivox.LoggedIn += LocalUserLoggedIn;
                BindToParticipantEvents();
            }
            catch (System.Exception e)
            {
                m_IsInitialized = false;
                m_IsVivoxAvailable = false;
                m_ConnectionStatus.Value = "Voice Service Unavailable";
#if UNITY_EDITOR
                EditorGUI.hyperLinkClicked += HyperlinkClicked;
                Utils.Log($"{k_DebugPrepend}Vivox Initialization Failed. Please check the Vivox Service Window <a data=\"OpenVivoxSettings\"><b>Project Settings > Services > Vivox</b></a>\n\n{e}", 2);
#else
                Utils.Log($"{k_DebugPrepend}Vivox Initialization Failed.\n\n{e}", 2);
#endif
            }
        }

#if UNITY_EDITOR
        void HyperlinkClicked(EditorWindow window, HyperLinkClickedEventArgs args)
        {
            if(args.hyperLinkData.ContainsValue("OpenVivoxSettings"))
            {
                SettingsService.OpenProjectSettings("Project/Services/Vivox");
            }
        }
#endif

        async void Login()
        {
            if (!m_IsInitialized || !m_IsVivoxAvailable || XRINetworkGameManager.Instance.sessionManager.currentSession == null)
            {
                return;
            }

            var vivox = VivoxService.Instance;
            if (vivox == null)
            {
                return;
            }

            var displayName = XRINetworkGameManager.AuthenicationId;
            m_CurrentLobbyId = XRINetworkGameManager.Instance.sessionManager.currentSession.Id;

            LoginOptions loginOptions = new()
            {
                DisplayName = displayName,
                ParticipantUpdateFrequency = m_UpdateFrequency
            };

            if (vivox.IsLoggedIn)
            {
                Utils.Log($"{k_DebugPrepend}Logging out of Voice Chat");
                m_ConnectionStatus.Value = "Logging out of Voice Chat";
                await vivox.LogoutAsync();
            }

            if (!vivox.IsLoggedIn)
            {
                Utils.Log($"{k_DebugPrepend}Logging In to room {m_CurrentLobbyId} as {displayName}");
                m_ConnectionStatus.Value = "Logging In To Voice Service";
                await vivox.LoginAsync(loginOptions);
            }
            else
            {
                Utils.Log($"{k_DebugPrepend}Attempting to login to voice chat while already logged in.", 1);
            }
        }

        void LocalUserLoggedIn()
        {
            var vivox = VivoxService.Instance;
            if (vivox != null && vivox.IsLoggedIn)
            {
                Utils.Log($"{k_DebugPrepend}Local User Logged In to Voice Chat.");
                m_ConnectionStatus.Value = "Joining Voice Channel";
                ConnectToVoiceChannel();
            }
        }

        async void ConnectToVoiceChannel()
        {
            var vivox = VivoxService.Instance;
            if (vivox == null)
            {
                return;
            }

            if (NetworkManager.Singleton.IsConnectedClient && !m_ConnectedToRoom)
            {
                Channel3DProperties properties = new(AudibleDistance, ConversationalDistance, AudioFadeIntensity, AudioFadeModel);
                Utils.Log($"{k_DebugPrepend}Joining Voice Channel: {m_CurrentLobbyId}, properties: {properties}");
                await vivox.JoinPositionalChannelAsync(m_CurrentLobbyId, m_ChatCapability, properties);

                // Once connecting, make sure we are still in the game session, if not, disconnect from the voice chat.
                if (!NetworkManager.Singleton.IsConnectedClient)
                {
                    Disconnect();
                }
            }
            else
            {
                Utils.Log($"{k_DebugPrepend}Failed to join Voice Chat, Player is not connected to a game", 1);
            }
        }

        void BindToParticipantEvents()
        {
            var vivox = VivoxService.Instance;
            if (vivox == null)
            {
                return;
            }

            vivox.ParticipantAddedToChannel += OnParticipantAdded;
            vivox.ParticipantRemovedFromChannel += OnParticipantRemoved;
        }

        void UnbindParticipantEvents()
        {
            var vivox = VivoxService.Instance;
            if (vivox == null)
            {
                return;
            }

            vivox.ParticipantAddedToChannel -= OnParticipantAdded;
            vivox.ParticipantRemovedFromChannel -= OnParticipantRemoved;
        }

        async void DisconnectAsync()
        {
            var vivox = VivoxService.Instance;
            if (vivox == null)
            {
                return;
            }

            m_ConnectionStatus.Value = "Leaving current channel";
            await vivox.LeaveAllChannelsAsync();
        }

        [ContextMenu("Reconnect")]
        public void Reconnect()
        {
            if (XRINetworkGameManager.CurrentSessionType == SessionType.LocalOnly)
            {
                Utils.Log($"{k_DebugPrepend}Cannot reconnect in Local Only session.", 1);
                return;
            }
            ReconnectAsync();
        }

        async void ReconnectAsync()
        {
            var vivox = VivoxService.Instance;
            if (vivox == null)
            {
                return;
            }

            m_ConnectionStatus.Value = "Leaving current channel";
            await vivox.LeaveAllChannelsAsync();

            if (vivox.IsLoggedIn)
            {
                ConnectToVoiceChannel();
            }
            else
            {
                m_ConnectionStatus.Value = "Reconnecting to Voice Chat";
                Login();
            }
        }

        void LogOut()
        {
            var vivox = VivoxService.Instance;
            Utils.Log($"{k_DebugPrepend}Logging out of Voice Chat.");
            if (vivox != null && vivox.IsLoggedIn && m_ConnectedToRoom)
            {
                m_ConnectedToRoom = false;
                vivox.LeaveAllChannelsAsync();
                vivox.LogoutAsync();
            }

            m_PlayersDictionary.Clear();
        }

        public void Set3DAudio(Transform localPlayerHeadTransform)
        {
            var vivox = VivoxService.Instance;
            if (vivox != null && vivox.IsLoggedIn && vivox.ActiveChannels.Count > 0 && vivox.TransmittingChannels[0] == m_CurrentLobbyId)
            {
                vivox.Set3DPosition(localPlayerHeadTransform.position,
                    localPlayerHeadTransform.position,
                    localPlayerHeadTransform.forward,
                    localPlayerHeadTransform.up,
                    m_CurrentLobbyId);
            }
        }


        public void ToggleSelfMute(bool setManual = false, bool mutedOverrideValue = false)
        {
            if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                m_SelfMuted.Value = setManual ? mutedOverrideValue : !m_SelfMuted.Value;
            }
            else
            {
                m_SelfMuted.Value = false;
            }

            var vivox = VivoxService.Instance;
            if (XRINetworkGameManager.CurrentSessionType == SessionType.DistributedAuthority && vivox != null && vivox.IsLoggedIn)
            {
                if (m_SelfMuted.Value)
                {
                    vivox.MuteInputDevice();
                }
                else
                {
                    vivox.UnmuteInputDevice();
                }
            }
            else
            {
                OfflinePlayerAvatar.muted = m_SelfMuted.Value;
            }

            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                PlayerHudNotification.Instance.ShowText(k_MicrophonePersmissionDialogue, 3.0f);
            }
        }

        public void SetInputVolume(float volume)
        {
            var vivox = VivoxService.Instance;
            if (vivox == null)
            {
                return;
            }

            volume = Mathf.Clamp(volume, m_MinMaxVoiceInputVolume.x, m_MinMaxVoiceInputVolume.y);

            vivox.SetInputDeviceVolume((int)volume);

            // Since the slider goes to .001 percent, add a buffer to mute the mic
            if (volume <= (m_MinMaxVoiceInputVolume.x + .05f))
            {
                ToggleSelfMute(true, true);
            }
            else
            {
                ToggleSelfMute(true, false);
            }
        }

        public void SetOutputVolume(float volume)
        {
            var vivox = VivoxService.Instance;
            if (vivox == null)
            {
                return;
            }

            volume = Mathf.Clamp(volume, m_MinMaxVoiceOutputVolume.x, m_MinMaxVoiceOutputVolume.y);
            vivox.SetOutputDeviceVolume((int)volume);
        }

        void OnParticipantAdded(VivoxParticipant participant)
        {
            if (participant.IsSelf)
            {
                m_ConnectedToRoom = true;
                m_LocalParticpant = participant;
                m_SelfMuted.Value = false;
                XRINetworkPlayer.LocalPlayer.SetVoiceId(m_LocalParticpant.PlayerId);
                Utils.Log($"{k_DebugPrepend}Joined Voice Channel: {m_CurrentLobbyId}");
                m_ConnectionStatus.Value = "Joined Voice Channel";
                PlayerHudNotification.Instance.ShowText("Joined Voice Chat", 3.0f);
            }
            else
            {
                Utils.Log($"{k_DebugPrepend}Non-Local Player Joined Voice Channel: {participant.PlayerId}");
                foreach (XRINetworkPlayer player in FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None))
                {
                    if (player.playerVoiceId == participant.PlayerId)
                    {
                        player.SetupVoicePlayer();
                    }
                }
            }
        }

        void OnParticipantRemoved(VivoxParticipant participant)
        {
            RemoveVivoxPlayer(participant.PlayerId);
            if (participant.IsSelf)
            {
                Utils.Log($"{k_DebugPrepend}Left Voice Channel: {m_CurrentLobbyId}");
                m_ConnectionStatus.Value = "Left Voice Channel";
                m_ConnectedToRoom = false;
                m_LocalParticpant = null;
                PlayerHudNotification.Instance.ShowText("Voice Chat Disconnected", 3.0f);
            }
        }

        public VivoxParticipant GetVivoxParticipantById(string participantPlayerId)
        {
            var vivox = VivoxService.Instance;
            if (vivox == null || !vivox.ActiveChannels.ContainsKey(m_CurrentLobbyId))
            {
                return null;
            }

            foreach (var participant in vivox.ActiveChannels[m_CurrentLobbyId])
            {
                if (participantPlayerId == participant.PlayerId)
                    return participant;
            }
            return null;
        }

        // Gets called as soon as participant ID is synced
        public static void AddNewVivoxPlayer(string participantID, XRINetworkPlayer networkPlayer)
        {
            if (!m_PlayersDictionary.ContainsKey(participantID))
            {
                m_PlayersDictionary.Add(participantID, networkPlayer);
            }
            else
            {
                Utils.Log($"{k_DebugPrepend}Attempting to load multiple players with same id {participantID}", 1);
            }
        }

        public static void RemoveVivoxPlayer(string participantID)
        {
            if (participantID == XRINetworkPlayer.LocalPlayer.playerVoiceId)
            {
                Utils.Log($"{k_DebugPrepend}Local Player Left Voice Chat.");
                return;
            }
            if (m_PlayersDictionary.ContainsKey(participantID))
            {
                m_PlayersDictionary.Remove(participantID);
            }
        }

        [ContextMenu("Disconnect")]
        public void Disconnect()
        {
            DisconnectAsync();
        }

        [ContextMenu("Debug Particpants")]
        void DebugParticipants()
        {
            var vivox = VivoxService.Instance;
            if (vivox == null || !vivox.ActiveChannels.ContainsKey(m_CurrentLobbyId))
            {
                Utils.Log($"{k_DebugPrepend}No active Vivox channel to debug.", 1);
                return;
            }

            StringBuilder output = new StringBuilder();
            output.Append($"[Room Type: Positional\n[Room Code: {m_CurrentLobbyId}]");
            foreach (var participant in vivox.ActiveChannels[m_CurrentLobbyId])
            {
                output.Append($"\n[ParticipantID: {participant.PlayerId}]\n[AudioEnergy: {participant.AudioEnergy}]");
            }
            Utils.Log($"{k_DebugPrepend}{output}");
        }
    }
}
