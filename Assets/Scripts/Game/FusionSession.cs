using System;
using System.Threading.Tasks;
using Fusion;
using NightSupermarket.Core;
using UnityEngine;
namespace NightSupermarket.Game
{
    /// <summary>
    /// Starts a Fusion host session against the Photon App Id in PhotonAppSettings.
    /// Host remains the match authority. Clients are not simulated here yet.
    /// </summary>
    public sealed class FusionSession : MonoBehaviour, INetworkService
    {
        public const string PlaySessionName = "NightSupermarket";
        public NetworkRunner Runner { get; private set; }
        public StartGameResult LastResult { get; private set; }
        public bool Connected { get; private set; }
        public bool CloudReady => Runner != null && Runner.IsCloudReady;
        public bool LastOk => LastResult != null && LastResult.Ok;
        public string Status { get; private set; } = "OFFLINE";
        public bool IsAuthority => Runner == null || !Runner.IsRunning || Runner.IsServer;
        public string Mode => Connected ? "FUSION_HOST" : "LOCAL_TEST_MODE";

        /// <summary>Batch PlayMode scene tests stay local. Editor Play and the connect test call this.</summary>
        public Task<bool> ConnectHost(string sessionName = PlaySessionName) => Connect(GameMode.Host, sessionName);

        public async Task<bool> Connect(GameMode mode, string sessionName)
        {
            if (Runner != null && Runner.IsRunning) return Connected;
            Status = "CONNECTING";
            if (Runner == null) Runner = gameObject.AddComponent<NetworkRunner>();
            Runner.ProvideInput = true;
            if (Runner.GetComponent<NetworkSceneManagerDefault>() == null) gameObject.AddComponent<NetworkSceneManagerDefault>();
            if (Runner.GetComponent<NetworkObjectProviderDefault>() == null) gameObject.AddComponent<NetworkObjectProviderDefault>();
            try
            {
                LastResult = await Runner.StartGame(new StartGameArgs
                {
                    GameMode = mode,
                    SessionName = sessionName,
                });
            }
            catch (Exception exception)
            {
                LastResult = null;
                Status = exception.GetBaseException().Message;
                Connected = false;
                Debug.Log("[FUSION] Host session failed: " + Status);
                return false;
            }
            Connected = LastResult != null && LastResult.Ok && Runner != null && Runner.IsRunning;
            Status = Connected ? "CONNECTED" : Describe(LastResult);
            Debug.Log(Connected
                ? "[FUSION] Host session connected."
                : "[FUSION] Host session failed: " + Status);
            return Connected;
        }

        public void Shutdown()
        {
            if (Runner != null && Runner.IsRunning) Runner.Shutdown();
            Connected = false;
            Status = "OFFLINE";
        }

        private static string Describe(StartGameResult result)
        {
            if (result == null) return "NO_RESULT";
            if (result.Ok) return "CONNECTED";
            string detail = string.IsNullOrEmpty(result.ErrorMessage) ? result.ShutdownReason.ToString() : result.ShutdownReason + " " + result.ErrorMessage;
            return detail.Trim();
        }
    }
}
