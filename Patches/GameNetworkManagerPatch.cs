using HarmonyLib;
using TTSCompany.Components;

namespace TTSCompany.Patches
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
