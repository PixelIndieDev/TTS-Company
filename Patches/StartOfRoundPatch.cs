using HarmonyLib;
using TTSCompany.Components.Networking;

namespace TTSCompany.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    internal static class StartOfRoundPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnPlayerConnectedClientRpc")]
        private static void SyncTTSAudioSources(ulong clientId)
        {
            TTSCompanyNetworking.SyncActiveAudioSourcesTo(clientId);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnClientDisconnect")]
        private static void OnPlayerDisconnected(ulong clientId)
        {
            TTSCompanyNetworking.HandlePlayerDisconnected(clientId);
        }
    }
}
