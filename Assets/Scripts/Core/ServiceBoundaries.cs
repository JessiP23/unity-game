using System;
using System.Collections.Generic;
namespace NightSupermarket.Core
{
    /// <summary>Transport boundary. Local test mode is authoritative in-process. Fusion replaces this later.</summary>
    public interface INetworkService
    {
        bool IsAuthority { get; }
        string Mode { get; }
    }
    public sealed class LocalNetworkService : INetworkService
    {
        public bool IsAuthority => true;
        public string Mode => "LOCAL_TEST_MODE";
    }
    public interface ILobbyService
    {
        string Create(string name);
        bool TryJoin(string lobbyId, string connectionId);
    }
    public sealed class LocalLobbyService : ILobbyService
    {
        private readonly Dictionary<string, HashSet<string>> rooms = new Dictionary<string, HashSet<string>>();
        public string Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException(nameof(name));
            string id = Guid.NewGuid().ToString("N");
            rooms[id] = new HashSet<string>();
            return id;
        }
        public bool TryJoin(string lobbyId, string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId) || lobbyId == null || !rooms.TryGetValue(lobbyId, out var members)) return false;
            return members.Add(connectionId);
        }
    }
    /// <summary>Account data only. Match progress must not be stored here.</summary>
    public sealed class PlayerProfile
    {
        public string Id { get; }
        public float LookSensitivity { get; set; } = 0.12f;
        public PlayerProfile(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(nameof(id));
            Id = id;
        }
    }
    public interface IPlayerDataService
    {
        bool TryLoad(string profileId, out PlayerProfile profile);
        bool TrySave(PlayerProfile profile);
    }
    public sealed class MemoryPlayerDataService : IPlayerDataService
    {
        private readonly Dictionary<string, float> sensitivity = new Dictionary<string, float>();
        public bool TryLoad(string profileId, out PlayerProfile profile)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(profileId) || !sensitivity.TryGetValue(profileId, out float value)) return false;
            profile = new PlayerProfile(profileId) { LookSensitivity = value };
            return true;
        }
        public bool TrySave(PlayerProfile profile)
        {
            if (profile == null) return false;
            sensitivity[profile.Id] = profile.LookSensitivity;
            return true;
        }
    }
}
