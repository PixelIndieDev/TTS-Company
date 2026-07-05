using HarmonyLib;
using TTS_Company.Components;

namespace TTS_Company.Patches
{
    [HarmonyPatch(typeof(GameNetworkManager))]
    internal static class GameNetworkManagerPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("ResetGameValuesToDefault")]
        private static void OnReturnedToMainMenu()
        {
            TTSCompanyBackend.OnReturnedToMainMenu();
        }
    }
}
