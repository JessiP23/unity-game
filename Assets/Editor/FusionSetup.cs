using System;
using System.IO;
using Fusion;
using Fusion.Editor;
using Fusion.Photon.Realtime;
using UnityEditor;
using UnityEngine;
namespace NightSupermarket.Editor
{
    /// <summary>Writes the local Photon App Id into Fusion's PhotonAppSettings. The Id never lives in source.</summary>
    public static class FusionSetup
    {
        public const string LocalIdPath = ".photon-app-id";

        [MenuItem("Night Supermarket/Setup/Apply Photon App Id")]
        public static void ApplyAppId()
        {
            string id = ReadAppId();
            if (string.IsNullOrWhiteSpace(id) || !Guid.TryParse(id, out _))
                throw new InvalidOperationException("Put a Fusion App Id in .photon-app-id (gitignored) or PHOTON_FUSION_APP_ID.");
            FusionGlobalScriptableObjectUtils.EnsureAssetExists<PhotonAppSettings>();
            FusionGlobalScriptableObjectUtils.EnsureAssetExists<NetworkProjectConfigAsset>();
            if (!PhotonAppSettings.TryGetGlobal(out var settings) || settings == null)
                throw new InvalidOperationException("PhotonAppSettings was not created.");
            settings.AppSettings.AppIdFusion = id.Trim();
            settings.AppSettings.UseNameServer = true;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[SETUP] Photon App Id written to PhotonAppSettings.");
        }

        public static string ReadAppId()
        {
            string env = Environment.GetEnvironmentVariable("PHOTON_FUSION_APP_ID");
            if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
            string path = Path.Combine(Directory.GetCurrentDirectory(), LocalIdPath);
            if (!File.Exists(path)) return null;
            foreach (string line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith("#")) return trimmed;
            }
            return null;
        }
    }
}
