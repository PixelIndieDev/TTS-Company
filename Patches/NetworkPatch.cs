using HarmonyLib;
using System.Linq;
using System.Reflection;
using TTS_Company.Components.Constants;
using Unity.Netcode;
using UnityEngine;

namespace TTS_Company.Patches
{
    [HarmonyPatch(typeof(NetworkManager))]
    internal static class NetworkPatch
    {
        internal static GameObject TTSSyncPrefab { get; private set; }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(NetworkManager.SetSingleton))]
        private static void RegisterPrefab()
        {
            if (TTSSyncPrefab != null) return;

            TTSSyncPrefab = new GameObject(ModInfo.modGUID + " Prefab");
            TTSSyncPrefab.hideFlags |= HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(TTSSyncPrefab);

            var networkObject = TTSSyncPrefab.AddComponent<NetworkObject>();

            var fieldInfo = typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic);
            fieldInfo.SetValue(networkObject, GetHash(ModInfo.modGUID));

            NetworkManager.Singleton.PrefabHandler.AddNetworkPrefab(TTSSyncPrefab);

            Plugin.logSource.LogInfo("[TTSSync] Registered TTSNetworkSyncManager prefab.");
            LogConstants.CODE_TRIGGERED.Log(nameof(NetworkPatch), nameof(RegisterPrefab));

            return;
        }

        private static uint GetHash(string value)
        {
            return value?.Aggregate(17u, (current, c) => unchecked((current * 31) ^ c)) ?? 0u;
        }
    }
}
